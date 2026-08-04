using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace Blazor.ExampleConsumer.EndToEndTests;

[Collection(DomSiteCollection.Name)]
[Trait("Category", "DOMEndToEnd")]
public sealed class DomInteropTests(
    BlazoratorsSiteFixture webAssemblySite,
    BlazorServerSiteFixture serverSite,
    BrowserFixture browser)
{
    public static IEnumerable<object[]> Hosts()
    {
        yield return [DomHost.WebAssembly];
        yield return [DomHost.Server];
    }

    public static IEnumerable<object[]> FilePreviewCases()
    {
        yield return
        [
            "Preview.cs",
            "text/plain",
            Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    "public sealed class Preview\n{\n    public string Status => \"Ready\";\n}")),
            "code"
        ];
        yield return
        [
            "pixel.png",
            "image/png",
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
            "image"
        ];
        yield return
        [
            "guide.pdf",
            "application/pdf",
            Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes(
                    "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF")),
            "pdf"
        ];
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task GeneratedDomInteropRunsInARealBrowser(DomHost host)
    {
        BlazorSiteFixture site = host is DomHost.WebAssembly ? webAssemblySite : serverSite;
        await using var context = await browser.Browser.NewContextAsync();
        await context.AddInitScriptAsync(
            """
            globalThis.__domE2EListeners = { added: 0, removed: 0 };
            const originalAdd = EventTarget.prototype.addEventListener;
            const originalRemove = EventTarget.prototype.removeEventListener;
            EventTarget.prototype.addEventListener = function (type, listener, options) {
                if (this instanceof Element && this.id === 'dom-event-target' && type === 'click') {
                    globalThis.__domE2EListeners.added++;
                }
                return originalAdd.call(this, type, listener, options);
            };
            EventTarget.prototype.removeEventListener = function (type, listener, options) {
                if (this instanceof Element && this.id === 'dom-event-target' && type === 'click') {
                    globalThis.__domE2EListeners.removed++;
                }
                return originalRemove.call(this, type, listener, options);
            };
            """);

        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/dom-e2e"));
        var root = page.Locator("#dom-e2e");
        await page.WaitForFunctionAsync(
            "() => ['ready', 'failed'].includes(document.querySelector('#dom-e2e')?.dataset.phase)");
        var phase = await root.GetAttributeAsync("data-phase");
        var error = phase is "failed"
            ? await page.Locator("[data-result='error']").TextContentAsync()
            : null;
        Assert.True(phase is "ready", $"{host} DOM validation failed: {error}");
        await Assertions.Expect(root).ToHaveAttributeAsync(
            "data-host",
            host is DomHost.WebAssembly ? "webassembly" : "server");

        await ExpectResultAsync(page, "di-root", "resolved");
        await ExpectResultAsync(page, "window", "read");
        await ExpectResultAsync(page, "document", "read");
        await ExpectResultAsync(page, "navigator", "read");
        await Assertions.Expect(Result(page, "performance")).ToHaveTextAsync(
            new Regex(@"^\d+\.\d{2} ms$"));
        await Assertions.Expect(Result(page, "crypto")).ToHaveTextAsync(
            new Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"));
        await Assertions.Expect(Result(page, "storage")).ToContainTextAsync(" quota");
        await Assertions.Expect(Result(page, "screen")).ToContainTextAsync("-bit color");
        await Assertions.Expect(Result(page, "media-devices")).ToContainTextAsync("deviceId=");
        await ExpectResultAsync(page, "mutable-property", "written");
        await ExpectResultAsync(page, "promise-union", "fulfilled");
        await ExpectResultAsync(page, "returned-proxy", "read");
        await ExpectResultAsync(page, "feature-detection", "available");
        await ExpectResultAsync(page, "js-error", "propagated");
        await ExpectResultAsync(page, "typed-event", "subscribed");

        if (host is DomHost.WebAssembly)
        {
            await ExpectResultAsync(page, "sync-before-init", "blocked");
            await ExpectResultAsync(page, "sync-after-init", "read");
            await Assertions.Expect(root.Locator("section.dom-section")).ToHaveCountAsync(5);
            await Assertions.Expect(root.Locator(".dom-section-nav a")).ToHaveCountAsync(5);
            await Assertions.Expect(root.Locator(".dom-package-card")).ToHaveCountAsync(14);
            await AssertCapabilityLinksAreBaseRelativeAsync(root.Locator(".dom-package-card"));
        }
        else
        {
            await ExpectResultAsync(page, "lazy-init", "interactive-only");
        }

        await page.Locator("#dom-event-target").ClickAsync();
        await ExpectResultAsync(page, "event-count", "1");
        await ExpectResultAsync(page, "event-type", "click");

        await page.Locator("#dom-dispose").ClickAsync();
        await ExpectResultAsync(page, "borrowed-release", "released-once");
        await ExpectResultAsync(page, "proxy-release", "released-once");

        await page.Locator("#dom-event-target").ClickAsync();
        await ExpectResultAsync(page, "event-count", "1");

        var listenerCounts = await page.EvaluateAsync<ListenerCounts>(
            "() => globalThis.__domE2EListeners");
        Assert.Equal(1, listenerCounts.Added);
        Assert.Equal(1, listenerCounts.Removed);
    }

    [Fact]
    public async Task ServerPrerenderDoesNotAttemptDomInterop()
    {
        using var client = new HttpClient();
        var html = await client.GetStringAsync(serverSite.UrlFor("/dom-e2e"));

        Assert.Contains("data-host=\"server\"", html, StringComparison.Ordinal);
        Assert.Contains("data-phase=\"prerender\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-result=\"error\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebAssemblyLabBookmarksUseClientSideRouting()
    {
        await using var context = await browser.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e#packages"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#dom-e2e')?.dataset.phase === 'ready'");

        await Assertions.Expect(page.Locator("#packages")).ToBeFocusedAsync();
        await Assertions.Expect(
            page.Locator(".dom-section-nav a[aria-current='location']"))
            .ToHaveTextAsync("Package families");

        await page.Locator(".dom-section-nav a", new() { HasTextString = "Runtime roots" })
            .ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"#runtime$"));
        await Assertions.Expect(page.Locator("#runtime")).ToBeFocusedAsync();

        await page.GoBackAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"#packages$"));
        await Assertions.Expect(page.Locator("#packages")).ToBeFocusedAsync();
        await page.WaitForFunctionAsync(
            """
            () => {
                const top = document.querySelector('#packages')?.getBoundingClientRect().top;
                return top !== undefined && top >= 0 && top <= 240;
            }
            """);
        var top = await page.Locator("#packages")
            .EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        Assert.InRange(top, 0, 240);

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#dom-e2e')?.dataset.phase === 'ready'");
        await page.Locator(
                ".dom-section-nav a",
                new() { HasTextString = "Capabilities" })
            .ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"#capabilities$"));
        await page.GoBackAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/dom-e2e$"));
        await Assertions.Expect(page.Locator("#runtime")).ToBeFocusedAsync();
        await Assertions.Expect(
            page.Locator(".dom-section-nav a[aria-current='location']"))
            .ToHaveTextAsync("Runtime roots");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task CapabilityCatalogGroupLinksStayOnTheCatalogPage()
    {
        await using var context = await browser.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/capabilities"));

        var groupLinks = page.Locator(".catalog-jump-nav a");
        await Assertions.Expect(groupLinks).ToHaveCountAsync(3);
        await Assertions.Expect(
            page.Locator(".catalog-jump-nav a[aria-current='location']"))
            .ToHaveTextAsync("Essentials");

        foreach (var category in new[] { "Essentials", "Platform", "Specialized" })
        {
            var section = category.ToLowerInvariant();
            var link = groupLinks.GetByText(category, new() { Exact = true });

            await Assertions.Expect(link).ToHaveAttributeAsync(
                "href",
                $"dom-e2e/capabilities#{section}");
            await link.ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(
                new Regex($@"/dom-e2e/capabilities#{section}$"));
            await Assertions.Expect(page.Locator($"#{section}")).ToBeFocusedAsync();
            await Assertions.Expect(
                page.Locator(".catalog-jump-nav a[aria-current='location']"))
                .ToHaveTextAsync(category);
        }

        await page.GoBackAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"#platform$"));
        await Assertions.Expect(page.Locator("#platform")).ToBeFocusedAsync();
        await Assertions.Expect(
            page.Locator(".catalog-jump-nav a[aria-current='location']"))
            .ToHaveTextAsync("Platform");
    }

    [Fact]
    public async Task CapabilityCatalogRoutesToAnInteractiveGeneratedDemo()
    {
        await using var context = await browser.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/capabilities"));
        await Assertions.Expect(page.Locator("[data-capability]")).ToHaveCountAsync(14);
        await AssertCapabilityLinksAreBaseRelativeAsync(page.Locator("[data-capability]"));

        await page.Locator("[data-capability='web-crypto']").ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/dom-e2e/web-crypto$"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-probe-phase]')?.dataset.probePhase === 'ready'");

        await Assertions.Expect(page.Locator(".setup-grid article")).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator(".nav-submenu .nav-subitem")).ToHaveCountAsync(14);
        await AssertCapabilityLinksAreBaseRelativeAsync(
            page.Locator(".detail-pagination a[href]"));
        await Assertions.Expect(page.Locator("#dom-web-crypto")).ToHaveClassAsync(
            new Regex(@"\bactive\b"));
        await Assertions.Expect(page.Locator(".setup-grid")).ToContainTextAsync(
            "dotnet add package Blazor.WebCrypto.WebAssembly");
        await Assertions.Expect(page.Locator(".setup-grid")).ToContainTextAsync(
            "AddWebCryptoCapability()");
        await Assertions.Expect(page.Locator(".setup-grid")).ToContainTextAsync(
            "var builder = WebAssemblyHostBuilder.CreateDefault(args);");
        await Assertions.Expect(page.Locator(".setup-grid")).ToContainTextAsync(
            "await host.RunAsync();");
        await Assertions.Expect(page.Locator(".setup-grid")).ToContainTextAsync(
            "@inject IWebCryptoCapability Capability");
        var setupTops = await page.Locator(".setup-grid article").EvaluateAllAsync<double[]>(
            "articles => articles.map(article => article.getBoundingClientRect().top)");
        Assert.True(
            setupTops.SequenceEqual(setupTops.OrderBy(top => top))
            && setupTops.Distinct().Count() == setupTops.Length,
            "Install, register, and inject steps must be vertically stacked.");

        var componentCode = page.Locator(".detail-code .code-snippet code");
        await Assertions.Expect(componentCode).ToContainTextAsync(
            "var id = crypto.RandomUUID();");
        await Assertions.Expect(componentCode).ToContainTextAsync(
            "@inject IWebCryptoCapability Capability");
        await Assertions.Expect(componentCode).ToContainTextAsync(
            "private async Task RunAsync()");

        await page.Locator("#capability-action").ClickAsync();
        await Assertions.Expect(page.Locator("[data-demo-result='web-crypto']")).ToHaveTextAsync(
            new Regex(@"Generated secure UUID [0-9a-f-]{36}\."));
        await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
            "\"operation\": \"crypto.randomUUID()\"");
        await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
            new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}"));

        await page.GoBackAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/dom-e2e/capabilities$"));
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { Name = "DOM capability catalog" })).ToBeVisibleAsync();

        await page.Locator("[data-capability='browser-coordination']").ClickAsync();
        await Assertions.Expect(page.Locator(".detail-code")).ToContainTextAsync(
            "Capability.GetBroadcastChannel()");
        await Assertions.Expect(page.Locator(".detail-code"))
            .Not.ToContainTextAsync("GetBroadcastChannelFactory");
        Assert.Empty(errors);
    }

    static async Task AssertCapabilityLinksAreBaseRelativeAsync(ILocator links)
    {
        var hrefs = await links.EvaluateAllAsync<string[]>(
            "elements => elements.map(element => element.getAttribute('href') ?? '')");

        Assert.NotEmpty(hrefs);
        Assert.All(
            hrefs,
            href => Assert.True(
                href.StartsWith("dom-e2e/", StringComparison.Ordinal),
                $"Capability link '{href}' must be relative to the application's base path."));
    }

    [Fact]
    public async Task UnsupportedBrowserEntryPointsRemainUsableUnavailablePages()
    {
        (string Slug, string Path)[] cases =
        [
            ("wake-lock", "navigator.wakeLock"),
            ("browser-coordination", "navigator.locks"),
            ("media-devices", "navigator.mediaDevices"),
            ("notifications", "Notification"),
            ("file-system-access", "window.showOpenFilePicker"),
        ];

        foreach (var (slug, path) in cases)
        {
            await using var context = await browser.Browser.NewContextAsync();
            var descriptor = slug == "wake-lock"
                ? """
                  {
                      configurable: true,
                      get() {
                          throw new DOMException("blocked", "SecurityError");
                      }
                  }
                  """
                : "{ configurable: true, value: undefined }";
            await context.AddInitScriptAsync(
                $$"""
                (() => {
                    const parts = "{{path}}".split(".");
                    let target = globalThis;
                    if (parts[0] === "window") {
                        parts.shift();
                    }
                    const property = parts.pop();
                    for (const part of parts) {
                        target = target?.[part];
                    }
                    if (target && property) {
                        Object.defineProperty(target, property, {{descriptor}});
                    }
                })();
                """);
            var page = await context.NewPageAsync();
            var errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);

            await page.GotoAsync(webAssemblySite.UrlFor($"/dom-e2e/{slug}"));

            await Assertions.Expect(page.Locator("[data-probe-phase]"))
                .ToHaveAttributeAsync("data-probe-phase", "unavailable");
            await Assertions.Expect(page.Locator("[data-probe-result='reading']"))
                .ToContainTextAsync(path);
            await Assertions.Expect(page.Locator("#capability-action"))
                .ToBeDisabledAsync();
            Assert.Empty(errors);
        }
    }

    [Theory]
    [MemberData(nameof(FilePreviewCases))]
    public async Task FileSystemAccess_PreviewsKnownFilesWithoutRetainingBrowserProxies(
        string fileName,
        string mimeType,
        string base64Content,
        string previewKind)
    {
        await using var context = await browser.Browser.NewContextAsync();
        var fileNameJson = System.Text.Json.JsonSerializer.Serialize(fileName);
        var mimeTypeJson = System.Text.Json.JsonSerializer.Serialize(mimeType);
        var contentJson = System.Text.Json.JsonSerializer.Serialize(base64Content);
        await context.AddInitScriptAsync(
            $$"""
            (() => {
                const bytes = Uint8Array.from(
                    atob({{contentJson}}),
                    character => character.charCodeAt(0));
                const file = new File([bytes], {{fileNameJson}}, {
                    type: {{mimeTypeJson}},
                    lastModified: Date.UTC(2026, 0, 15, 12, 0, 0)
                });
                Object.defineProperty(globalThis, 'showOpenFilePicker', {
                    configurable: true,
                    value: async () => [{
                        kind: 'file',
                        name: {{fileNameJson}},
                        getFile: async () => file
                    }]
                });
            })();
            """);
        var page = await context.NewPageAsync();

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/file-system-access"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-probe-phase]')?.dataset.probePhase === 'ready'");
        await page.Locator("#capability-action").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-file-preview-kind]') !== null",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        var preview = page.Locator($"[data-file-preview-kind='{previewKind}']");
        await Assertions.Expect(preview).ToBeVisibleAsync();
        await Assertions.Expect(preview).ToContainTextAsync(fileName);
        await Assertions.Expect(preview).ToContainTextAsync(
            "never uploaded");
        await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
            $"\"preview\": \"{previewKind}\"");

        if (previewKind == "code")
        {
            await Assertions.Expect(preview.Locator("code")).ToContainTextAsync(
                "public sealed class Preview");
        }
        else if (previewKind == "image")
        {
            await Assertions.Expect(preview.Locator("img")).ToHaveAttributeAsync(
                "src",
                new Regex("^data:image/png;base64,"));
        }
        else
        {
            await Assertions.Expect(preview.Locator("object")).ToHaveAttributeAsync(
                "data",
                new Regex("^data:application/pdf;base64,"));
        }
    }

    [Fact]
    public async Task CodeSnippetsKeepContentAndReportCopyFailureWithoutCdnAccess()
    {
        await using var context = await browser.Browser.NewContextAsync();
        await context.RouteAsync(
            new Regex(@"^https://esm\.sh/"),
            route => route.AbortAsync());
        await context.RouteAsync(
            new Regex(@"/clipboard\.js$"),
            route => route.FulfillAsync(
                new()
                {
                    Status = 200,
                    ContentType = "text/javascript",
                    Body = """
                           export function copyText() {
                               throw new Error("blocked");
                           }
                           """
                }));
        var page = await context.NewPageAsync();

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/web-crypto"));

        await Assertions.Expect(page.Locator(".detail-code .code-snippet code")).ToContainTextAsync(
            "var id = crypto.RandomUUID();");
        var copy = page.Locator(".detail-code .copy-btn");
        await copy.ClickAsync();
        await Assertions.Expect(copy).ToHaveAttributeAsync(
            "data-copy-status",
            "failed");
    }

    [Fact]
    public async Task EveryCapabilityPageExposesARunnableGeneratedDemo()
    {
        string[] actionSlugs =
        [
            "permissions",
            "clipboard",
            "web-share",
            "wake-lock",
            "storage-management",
            "screen",
            "performance",
            "web-crypto",
            "credentials",
            "offline-storage",
            "browser-coordination",
            "media-devices",
            "notifications"
        ];

        await using var context = await browser.Browser.NewContextAsync();
        var origin = new Uri(webAssemblySite.UrlFor("/")).GetLeftPart(UriPartial.Authority);
        await context.GrantPermissionsAsync(
            ["clipboard-read", "clipboard-write"],
            new() { Origin = origin });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);

        foreach (var slug in actionSlugs)
        {
            await page.GotoAsync(webAssemblySite.UrlFor($"/dom-e2e/{slug}"));
            await page.WaitForFunctionAsync(
                """
                () => ['ready', 'unavailable'].includes(
                    document.querySelector('[data-probe-phase]')?.dataset.probePhase)
                """);
            var phase = await page.Locator("[data-probe-phase]")
                .GetAttributeAsync("data-probe-phase");

            if (slug == "clipboard")
            {
                await page.Locator("#clipboard-demo-value").FillAsync("Automated DOM demo");
            }
            else if (slug == "web-share")
            {
                await page.Locator("#share-demo-value").FillAsync("Generated share payload");
            }

            var result = page.Locator($"[data-demo-result='{slug}']");
            if (phase == "unavailable")
            {
                await Assertions.Expect(page.Locator("#capability-action"))
                    .ToBeDisabledAsync();
                await Assertions.Expect(result).ToContainTextAsync(
                    "Browser entry point unavailable:");
            }
            else
            {
                await page.Locator("#capability-action").ClickAsync();
                await Assertions.Expect(result).ToHaveTextAsync(
                    ExpectedCapabilityResult(slug, phase));
            }

            if (phase == "ready"
                && slug == "clipboard"
                && !((await result.TextContentAsync()) ?? string.Empty).StartsWith(
                    "Browser response:",
                    StringComparison.Ordinal))
            {
                await page.Locator("#clipboard-paste-target").FocusAsync();
                await page.Keyboard.PressAsync("Control+V");
                await Assertions.Expect(page.Locator("[data-clipboard-verification]"))
                    .ToHaveAttributeAsync("data-clipboard-verification", "verified");
            }

            if (phase == "ready"
                && slug == "wake-lock"
                && ((await result.TextContentAsync()) ?? string.Empty).Contains(
                    "acquired",
                    StringComparison.OrdinalIgnoreCase))
            {
                await page.Locator("#capability-action").ClickAsync();
                await Assertions.Expect(result).ToContainTextAsync("released");
            }
            else if (phase == "ready" && slug == "offline-storage")
            {
                await page.Locator("#capability-action").ClickAsync();
                await Assertions.Expect(result).ToHaveTextAsync(
                    new Regex(@"^(?!Ready for interaction\.$|Running generated interop\.\.\.$).+"));
            }

            await Assertions.Expect(page.Locator(".raw-result")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
                $"\"capability\": \"{slug}\"");
            Assert.True(
                await page.Locator(".result-facts > div").CountAsync() > 0,
                $"{slug} should expose visual result facts.");
        }

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/file-system-access"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-probe-phase]')?.dataset.probePhase === 'ready'");
        await Assertions.Expect(page.Locator("#capability-action")).ToHaveTextAsync(
            "Choose a file");
        await Assertions.Expect(
            page.Locator("[data-demo-result='file-system-access']"))
            .ToHaveTextAsync("Ready for interaction.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task NotificationWorkflowRequestsPermissionAndCreatesTheAuthoredNotification()
    {
        await using var context = await browser.Browser.NewContextAsync();
        var origin = new Uri(webAssemblySite.UrlFor("/")).GetLeftPart(UriPartial.Authority);
        await context.GrantPermissionsAsync(
            ["notifications"],
            new() { Origin = origin });
        var page = await context.NewPageAsync();

        await page.GotoAsync(webAssemblySite.UrlFor("/dom-e2e/notifications"));
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-probe-phase]')?.dataset.probePhase === 'ready'");
        await page.Locator("#notification-demo-title").FillAsync("Build complete");
        await page.Locator("#notification-demo-body").FillAsync("All generated checks passed.");

        await page.Locator("#capability-action").ClickAsync();
        await Assertions.Expect(page.Locator("[data-demo-result='notifications']"))
            .ToContainTextAsync("granted");
        await Assertions.Expect(page.Locator("#notification-show-action")).ToBeVisibleAsync();

        await page.Locator("#notification-show-action").ClickAsync();
        await Assertions.Expect(page.Locator("[data-demo-result='notifications']"))
            .ToHaveTextAsync("Created a live browser notification with the entered values.");
        await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
            "\"operation\": \"new Notification(title, options)\"");
        await Assertions.Expect(page.Locator(".raw-result code")).ToContainTextAsync(
            "\"title\": \"Build complete\"");
        await Assertions.Expect(page.Locator(".detail-code")).ToContainTextAsync(
            "RequestPermissionAsync");
        await Assertions.Expect(page.Locator(".detail-code")).ToContainTextAsync(
            "factory.Create");
        await Assertions.Expect(page.Locator(".detail-code")).ToContainTextAsync(
            "Capability.GetNotification()");
        await Assertions.Expect(page.Locator(".detail-code"))
            .Not.ToContainTextAsync("GetNotificationFactory");
    }

    static Task ExpectResultAsync(IPage page, string name, string value) =>
        Assertions.Expect(Result(page, name)).ToHaveTextAsync(value);

    static Regex ExpectedCapabilityResult(string slug, string? phase) => slug switch
    {
        "permissions" => new(@"^(Camera|Geolocation|Microphone|Notifications|Persistent storage) is (granted|prompt|denied)\.$"),
        "clipboard" => new(@"^Copied \d+ characters\. Paste into step 3 to verify\.$"),
        "web-share" when phase == "unavailable" => new(@"^Browser response: .+"),
        "web-share" => new(@"^The browser (accepts this payload|does not expose a compatible share sheet).+"),
        "wake-lock" => new(@"^(Screen wake lock acquired\..+|Browser response: Wake Lock permission request denied)$"),
        "storage-management" => new(@"^.+ used of .+\.$"),
        "screen" => new(@"^\d+ × \d+ screen; \d+ × \d+ viewport\.$"),
        "performance" => new(@"^Requested \d+ ms; measured \d+\.\d{2} ms\.$"),
        "web-crypto" => new(@"^Generated secure UUID [0-9a-f-]{36}\.$"),
        "credentials" => new(
            @"^(Silent credential access is disabled for this origin\.|Browser response: The user agent does not support public key credentials\.)$"),
        "offline-storage" => new(@"^Cache '.+' was (created|removed); exists now=(true|false)\.$"),
        "browser-coordination" => new(@"^\d+ held and \d+ pending locks for this origin\.$"),
        "media-devices" => new(@"^The browser returned \d+ media devices?\.$"),
        "notifications" => new(@"^Notification permission is (default|denied|granted)\.$"),
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Unknown capability demo.")
    };

    static ILocator Result(IPage page, string name) =>
        page.Locator($"[data-result='{name}']");

    public enum DomHost
    {
        WebAssembly,
        Server
    }

    sealed class ListenerCounts
    {
        public int Added { get; set; }
        public int Removed { get; set; }
    }
}
