using Microsoft.Playwright;

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

    static Task ExpectResultAsync(IPage page, string name, string value) =>
        Assertions.Expect(page.Locator($"[data-result='{name}']")).ToHaveTextAsync(value);

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
