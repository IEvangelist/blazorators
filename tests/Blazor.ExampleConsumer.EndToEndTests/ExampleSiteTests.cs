using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Blazor.ExampleConsumer.EndToEndTests;

[Collection(ExampleSiteCollection.Name)]
public sealed class ExampleSiteTests(
    BlazoratorsSiteFixture site,
    BrowserFixture browser)
{
    static readonly PageRoute[] Routes =
    [
        new("/", "Blazorators", "Browser APIs"),
        new("/todos", "Local storage", "Local storage"),
        new("/geolocation", "Geolocation", "Geolocation"),
        new("/track", "Watch position", "Watch position"),
        new("/speak", "Text-to-speech", "Text-to-speech"),
        new("/listen", "Speech-to-text", "Speech-to-text"),
        new("/sandbox", "Sandbox", "Sandbox"),
        new("/audio", "Audio", "Audio")
    ];

    public static IEnumerable<object[]> RouteData() =>
        Routes.Select(route => new object[] { route });

    [Theory]
    [MemberData(nameof(RouteData))]
    public async Task Route_IsAccessible_Responsive_AndOverflowSafe(PageRoute route)
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var consoleErrors = TrackConsoleErrors(page);

        await page.GotoAsync(site.UrlFor(route.Path), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await ExpectHeadingAsync(page, route.Heading);
        Assert.Contains(route.TitleFragment, await page.TitleAsync(), StringComparison.OrdinalIgnoreCase);

        await AssertNoAxeViolationsAsync(page);
        await AssertNoDocumentOverflowAsync(page);
        await AssertNoConsoleErrorsAsync(consoleErrors);

        await page.SetViewportSizeAsync(390, 844);
        await AssertNoDocumentOverflowAsync(page);
        await AssertNoClippedVisibleTextAsync(page);
    }

    [Fact]
    public async Task SkipLink_IsFirstFocusableControl_AndMovesFocusToMainContent()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/track"));
        await ExpectHeadingAsync(page, "Watch position");

        var firstFocusableText = await page.EvaluateAsync<string>(
            """
            () => {
                const selector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
                return document.querySelector(selector)?.textContent?.trim() ?? '';
            }
            """);
        Assert.Equal("Skip to content", firstFocusableText);

        await page.Locator(".skip-link").FocusAsync();

        var activeText = await page.EvaluateAsync<string>("() => document.activeElement?.textContent?.trim() ?? ''");
        Assert.Equal("Skip to content", activeText);
        await Assertions.Expect(page.Locator(".skip-link")).ToBeInViewportAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.skip-link')?.getBoundingClientRect().top >= 0");

        var activeBox = await page.Locator(":focus").BoundingBoxAsync();
        Assert.NotNull(activeBox);
        Assert.True(activeBox!.Y >= 0, "Skip link should be visible when focused.");

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => location.pathname.replace(/\\/$/, '').endsWith('/track') && location.hash === '#main'");

        var activeId = await page.EvaluateAsync<string>("() => document.activeElement?.id ?? ''");
        Assert.Equal("main", activeId);
    }

    [Fact]
    public async Task MobileNavigation_AdvertisesExpandedState_AndClosesAfterNavigation()
    {
        await using var context = await NewContextAsync(viewportWidth: 390, viewportHeight: 844);
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");

        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Open navigation" });
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");

        await toggle.ClickAsync();

        var closeToggle = page.GetByRole(AriaRole.Button, new() { Name = "Close navigation" });
        await Assertions.Expect(closeToggle).ToHaveAttributeAsync("aria-expanded", "true");
        var primaryNav = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary" });
        await Assertions.Expect(primaryNav).ToBeVisibleAsync();

        await primaryNav.GetByRole(AriaRole.Link, new() { Name = "Local storage" }).ClickAsync();

        await ExpectHeadingAsync(page, "Local storage");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Open navigation" })).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Fact]
    public async Task TrackPage_AlignsMapAndLiveUpdatesPanel()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/track"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
        await ExpectHeadingAsync(page, "Watch position");

        var alignment = await page.EvaluateAsync<TrackAlignment>(
            """
            () => {
                const map = document.querySelector('.track-grid-map .map-shell');
                const timeline = document.querySelector('.track-grid-timeline');
                const mapRect = map.getBoundingClientRect();
                const timelineRect = timeline.getBoundingClientRect();

                return {
                    topDelta: Math.abs(mapRect.top - timelineRect.top),
                    mapHeight: mapRect.height,
                    timelineHeight: timelineRect.height
                };
            }
            """);

        Assert.True(alignment.TopDelta <= 1, $"Map and Live updates should start together; top delta was {alignment.TopDelta:0.##}px.");
        Assert.True(alignment.MapHeight >= 320, "The map should keep a useful desktop height.");
        Assert.True(alignment.TimelineHeight >= 320, "The Live updates panel should visually balance the map height.");
    }

    [Fact]
    public async Task HeroWordRotator_DoesNotClipOrOverlapTextDuringSwap()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");

        for (var i = 0; i < 12; i++)
        {
            var result = await page.EvaluateAsync<RotatorSnapshot>(
                """
                () => {
                    const rotator = document.querySelector('.word-rotator');
                    const rotatorRect = rotator.getBoundingClientRect();
                    const visible = [...rotator.querySelectorAll('span')]
                        .map(span => {
                            const style = getComputedStyle(span);
                            const rect = span.getBoundingClientRect();
                            return {
                                opacity: Number.parseFloat(style.opacity),
                                top: rect.top,
                                bottom: rect.bottom,
                                width: rect.width,
                                text: span.textContent.trim()
                            };
                        })
                        .filter(span => span.opacity > 0.05);

                    return {
                        visibleCount: visible.length,
                        rotatorTop: rotatorRect.top,
                        rotatorBottom: rotatorRect.bottom,
                        rotatorWidth: rotatorRect.width,
                        visible
                    };
                }
                """);

            Assert.InRange(result.VisibleCount, 0, 1);
            Assert.True(result.RotatorWidth > 0, "The word rotator should reserve width for the active word.");

            foreach (var span in result.Visible)
            {
                Assert.True(span.Top >= result.RotatorTop - 1, $"Rotating word '{span.Text}' is clipped above its container.");
                Assert.True(span.Bottom <= result.RotatorBottom + 1, $"Rotating word '{span.Text}' is clipped below its container.");
                Assert.True(span.Width <= result.RotatorWidth + 1, $"Rotating word '{span.Text}' is wider than its reserved container.");
            }

            await Task.Delay(300);
        }
    }

    [Fact]
    public async Task ReducedMotion_KeepsHeroReadableWithoutAnimations()
    {
        await using var context = await NewContextAsync(reducedMotion: ReducedMotion.Reduce);
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");

        var snapshot = await page.EvaluateAsync<ReducedMotionSnapshot>(
            """
            () => {
                const words = [...document.querySelectorAll('.word-rotator > span')]
                    .map(span => ({
                        text: span.textContent.trim(),
                        opacity: Number.parseFloat(getComputedStyle(span).opacity),
                        animationName: getComputedStyle(span).animationName
                    }));

                return {
                    visibleWords: words.filter(word => word.opacity > 0.9).map(word => word.text),
                    animationNames: words.map(word => word.animationName)
                };
            }
            """);

        Assert.Equal(new[] { "type-safe" }, snapshot.VisibleWords);
        Assert.All(snapshot.AnimationNames, animationName => Assert.Equal("none", animationName));
    }

    [Fact]
    public async Task ThemeToggle_PersistsAccessibleThemeSelection()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");

        var darkTheme = page.GetByRole(AriaRole.Button, new() { Name = "Dark theme" });
        await darkTheme.ClickAsync();

        await Assertions.Expect(darkTheme).ToHaveAttributeAsync("aria-pressed", "true");

        var stored = await page.EvaluateAsync<string>("() => localStorage.getItem('theme') ?? ''");
        var isDark = await page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('dark')");
        Assert.Equal("dark", stored);
        Assert.True(isDark);
    }

    [Fact]
    public async Task SpeechRecognitionControls_HaveNamesStatesAndLiveStatus()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        foreach (var path in new[] { "/listen", "/audio" })
        {
            await page.GotoAsync(site.UrlFor(path));
            await ExpectHeadingAsync(page, path == "/listen" ? "Speech-to-text" : "Audio");

            var mic = page.GetByRole(AriaRole.Button, new() { Name = "Start speech recognition" });
            await Assertions.Expect(mic).ToHaveAttributeAsync("aria-pressed", "false");
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Ready to listen");
        }
    }

    async Task<IBrowserContext> NewContextAsync(
        int viewportWidth = 1440,
        int viewportHeight = 1000,
        ReducedMotion reducedMotion = ReducedMotion.NoPreference)
    {
        var context = await browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = viewportWidth,
                Height = viewportHeight
            },
            ReducedMotion = reducedMotion,
            Geolocation = new Geolocation
            {
                Latitude = 47.6062f,
                Longitude = -122.3321f,
                Accuracy = 12f
            },
            Permissions = ["geolocation"]
        });

        await context.AddInitScriptAsync(
            """
            window.__blazoratorsConsoleErrors = [];
            const originalError = console.error;
            console.error = (...args) => {
                window.__blazoratorsConsoleErrors.push(args.map(String).join(' '));
                originalError(...args);
            };
            """);

        return context;
    }

    static List<string> TrackConsoleErrors(IPage page)
    {
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, exception) => errors.Add(exception);
        return errors;
    }

    static async Task ExpectHeadingAsync(IPage page, string heading)
    {
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading })).ToBeVisibleAsync();
    }

    static async Task AssertNoAxeViolationsAsync(IPage page)
    {
        var result = await page.RunAxe();
        var violations = result.Violations
            .Where(violation => violation.Impact is "critical" or "serious" or "moderate")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Expected no axe accessibility violations, but found: " +
            string.Join(Environment.NewLine, violations.Select(FormatViolation)));
    }

    static async Task AssertNoDocumentOverflowAsync(IPage page)
    {
        var overflow = await page.EvaluateAsync<DocumentOverflow>(
            """
            () => ({
                scrollWidth: document.documentElement.scrollWidth,
                clientWidth: document.documentElement.clientWidth,
                bodyScrollWidth: document.body.scrollWidth,
                bodyClientWidth: document.body.clientWidth
            })
            """);

        Assert.True(
            overflow.ScrollWidth <= overflow.ClientWidth + 1,
            $"Document horizontally overflows: scrollWidth={overflow.ScrollWidth}, clientWidth={overflow.ClientWidth}.");
        Assert.True(
            overflow.BodyScrollWidth <= overflow.BodyClientWidth + 1,
            $"Body horizontally overflows: scrollWidth={overflow.BodyScrollWidth}, clientWidth={overflow.BodyClientWidth}.");
    }

    static async Task AssertNoClippedVisibleTextAsync(IPage page)
    {
        var clipped = await page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('h1, h2, h3, p, label, button, a, .badge, .timeline-coords, .timeline-chip')]
                .filter(el => {
                    const style = getComputedStyle(el);
                    const rect = el.getBoundingClientRect();
                    if (rect.width === 0 || rect.height === 0 || style.visibility === 'hidden' || style.display === 'none') {
                        return false;
                    }

                    if (el.classList.contains('visually-hidden') || el.closest('.visually-hidden')) {
                        return false;
                    }

                    const allowsInternalScroll = el.closest('pre, code, .table-wrap, .codeblock, .bento-anim, .bento-preview');
                    if (allowsInternalScroll) {
                        return false;
                    }

                    return el.scrollWidth > el.clientWidth + 1 && style.overflowX !== 'visible';
                })
                .map(el => `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}.${[...el.classList].join('.')} "${el.textContent.trim().slice(0, 80)}"`)
            """);

        Assert.True(clipped.Length == 0, "Visible text should not be clipped: " + string.Join("; ", clipped));
    }

    static async Task AssertNoConsoleErrorsAsync(IReadOnlyCollection<string> errors)
    {
        await Task.Delay(50);
        Assert.True(errors.Count == 0, "Expected no browser console errors, but found: " + string.Join(Environment.NewLine, errors));
    }

    static string FormatViolation(AxeResultItem violation) =>
        $"{violation.Id} ({violation.Impact}): {violation.Description} Targets: " +
        string.Join(", ", violation.Nodes.Select(node => node.Target.ToString()));

    public sealed record PageRoute(string Path, string TitleFragment, string Heading)
    {
        public override string ToString() => Path;
    }

    sealed class DocumentOverflow
    {
        public int ScrollWidth { get; set; }
        public int ClientWidth { get; set; }
        public int BodyScrollWidth { get; set; }
        public int BodyClientWidth { get; set; }
    }

    sealed class RotatorSnapshot
    {
        public int VisibleCount { get; set; }
        public double RotatorTop { get; set; }
        public double RotatorBottom { get; set; }
        public double RotatorWidth { get; set; }
        public RotatorWordSnapshot[] Visible { get; set; } = [];
    }

    sealed class RotatorWordSnapshot
    {
        public double Opacity { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Width { get; set; }
        public string Text { get; set; } = "";
    }

    sealed class ReducedMotionSnapshot
    {
        public string[] VisibleWords { get; set; } = [];
        public string[] AnimationNames { get; set; } = [];
    }

    sealed class TrackAlignment
    {
        public double TopDelta { get; set; }
        public double MapHeight { get; set; }
        public double TimelineHeight { get; set; }
    }
}
