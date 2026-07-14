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

    static readonly string[] HeroPhrases =
    [
        "type-safe in C#.",
        "ergonomic in C#.",
        "promised in C#.",
        "generated in C#."
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
    public async Task TrackPage_RefreshIntervalControl_HasAccessibleToggleSemantics()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/track"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
        await ExpectHeadingAsync(page, "Watch position");

        // The toolbar wraps the segmented control with an accessible name + button-group role.
        var toolbar = page.Locator(".track-toolbar");
        await Assertions.Expect(toolbar).ToBeVisibleAsync();

        var group = page.GetByRole(AriaRole.Group, new() { Name = "Refresh" });
        await Assertions.Expect(group).ToBeVisibleAsync();

        // All five interval choices render, and Watch is the default pressed state.
        string[] expectedKeys = ["watch", "5s", "15s", "30s", "60s"];
        foreach (var key in expectedKeys)
        {
            var button = page.Locator($".track-toolbar button[data-interval='{key}']");
            await Assertions.Expect(button).ToBeVisibleAsync();
            var pressed = await button.GetAttributeAsync("aria-pressed");
            Assert.Equal(key == "watch" ? "true" : "false", pressed);
        }

        // The manual refresh trigger is a focusable, labelled button.
        var refreshNow = page.GetByRole(AriaRole.Button, new() { Name = "Refresh position now" });
        await Assertions.Expect(refreshNow).ToBeVisibleAsync();
        await refreshNow.FocusAsync();
        var focused = await page.EvaluateAsync<string?>(
            "() => document.activeElement?.getAttribute('aria-label')");
        Assert.Equal("Refresh position now", focused);

        // Selecting another interval mutually toggles aria-pressed.
        await page.Locator(".track-toolbar button[data-interval='15s']").ClickAsync();
        await Assertions.Expect(page.Locator(".track-toolbar button[data-interval='15s']"))
            .ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator(".track-toolbar button[data-interval='watch']"))
            .ToHaveAttributeAsync("aria-pressed", "false");

        // The header status badge swaps from WATCHING to POLLING.
        await Assertions.Expect(page.Locator("header.page-header .badge").First).ToContainTextAsync("POLLING");

        // Switching back to Watch restores the live subscription.
        await page.Locator(".track-toolbar button[data-interval='watch']").ClickAsync();
        await Assertions.Expect(page.Locator(".track-toolbar button[data-interval='watch']"))
            .ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator("header.page-header .badge").First).ToContainTextAsync("WATCHING");
    }

    [Fact]
    public async Task HeroTypewriter_DoesNotClipOrOverlapTextDuringTransition()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");

        var observedPhrases = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 12; i++)
        {
            var snapshot = await page.EvaluateAsync<TypewriterSnapshot>(
                """
                () => {
                    const typewriter = document.querySelector('.word-rotator');
                    const typewriterRect = typewriter.getBoundingClientRect();
                    const pieces = [...typewriter.children]
                        .map(element => {
                            const style = getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return {
                                className: element.className,
                                ariaHidden: element.getAttribute('aria-hidden'),
                                display: style.display,
                                visibility: style.visibility,
                                left: rect.left,
                                right: rect.right,
                                top: rect.top,
                                bottom: rect.bottom,
                                width: rect.width,
                                height: rect.height
                            };
                        })
                        .filter(piece =>
                            piece.ariaHidden !== 'true' &&
                            piece.display !== 'none' &&
                            piece.visibility !== 'hidden' &&
                            piece.width > 0 &&
                            piece.height > 0);

                    return {
                        ariaLabel: typewriter.getAttribute('aria-label') ?? '',
                        typewriterLeft: typewriterRect.left,
                        typewriterRight: typewriterRect.right,
                        typewriterTop: typewriterRect.top,
                        typewriterBottom: typewriterRect.bottom,
                        typewriterWidth: typewriterRect.width,
                        typewriterHeight: typewriterRect.height,
                        pieces
                    };
                }
                """);

            Assert.Contains(snapshot.AriaLabel, HeroPhrases);
            Assert.True(snapshot.TypewriterWidth > 0, "The typewriter should reserve width for the active phrase.");
            Assert.True(snapshot.TypewriterHeight > 0, "The typewriter should reserve height for the active phrase.");

            foreach (var piece in snapshot.Pieces)
            {
                Assert.True(piece.Left >= snapshot.TypewriterLeft - 1, $"Typewriter piece '{piece.ClassName}' is clipped on the left.");
                Assert.True(piece.Right <= snapshot.TypewriterRight + 1, $"Typewriter piece '{piece.ClassName}' is clipped on the right.");
                Assert.True(piece.Top >= snapshot.TypewriterTop - 1, $"Typewriter piece '{piece.ClassName}' is clipped above its container.");
                Assert.True(piece.Bottom <= snapshot.TypewriterBottom + 1, $"Typewriter piece '{piece.ClassName}' is clipped below its container.");
            }

            for (var pieceIndex = 1; pieceIndex < snapshot.Pieces.Length; pieceIndex++)
            {
                var previous = snapshot.Pieces[pieceIndex - 1];
                var current = snapshot.Pieces[pieceIndex];
                Assert.True(
                    current.Left >= previous.Right - 1,
                    $"Typewriter pieces '{previous.ClassName}' and '{current.ClassName}' overlap.");
            }

            observedPhrases.Add(snapshot.AriaLabel);
            await Task.Delay(300);
        }

        Assert.True(
            observedPhrases.Count > 1,
            "The typewriter should transition to another phrase during the test.");
    }

    [Fact]
    public async Task ReducedMotion_DisablesHeroCursorAnimation_AndKeepsTextAccessible()
    {
        await using var context = await NewContextAsync(reducedMotion: ReducedMotion.Reduce);
        var page = await context.NewPageAsync();
        await page.GotoAsync(site.UrlFor("/"));
        await ExpectHeadingAsync(page, "Browser APIs");
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('.word-rotator > .grad')?.textContent?.trim().length ?? 0) > 0");

        var snapshot = await page.EvaluateAsync<ReducedMotionSnapshot>(
            """
            () => {
                const typewriter = document.querySelector('.word-rotator');
                const word = typewriter.querySelector(':scope > .grad');
                const suffix = typewriter.querySelector(':scope > .typewriter-suffix');
                const cursor = typewriter.querySelector(':scope > .typewriter-cursor');
                const cursorStyle = getComputedStyle(cursor);
                const normalize = value => value.replace(/\u00a0/g, ' ').replace(/\s+/g, ' ').trim();

                return {
                    ariaLabel: typewriter.getAttribute('aria-label') ?? '',
                    wordText: normalize(word.textContent ?? ''),
                    suffixText: normalize(suffix?.textContent ?? ''),
                    cursorAnimationName: cursorStyle.animationName,
                    cursorOpacity: Number.parseFloat(cursorStyle.opacity)
                };
            }
            """);

        Assert.Contains(snapshot.AriaLabel, HeroPhrases);

        var expectedWord = snapshot.AriaLabel.Split(' ', 2)[0];
        Assert.True(
            expectedWord.StartsWith(snapshot.WordText, StringComparison.Ordinal),
            $"Visible typewriter text '{snapshot.WordText}' should be a prefix of '{expectedWord}'.");

        if (snapshot.SuffixText.Length > 0)
        {
            Assert.Equal(expectedWord, snapshot.WordText);
            Assert.True(
                "in C#.".StartsWith(snapshot.SuffixText, StringComparison.Ordinal),
                $"Visible typewriter suffix '{snapshot.SuffixText}' should be a prefix of 'in C#.'.");
        }

        Assert.Equal("none", snapshot.CursorAnimationName);
        Assert.InRange(snapshot.CursorOpacity, 0.99, 1);
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

    sealed class TypewriterSnapshot
    {
        public string AriaLabel { get; set; } = "";
        public double TypewriterLeft { get; set; }
        public double TypewriterRight { get; set; }
        public double TypewriterTop { get; set; }
        public double TypewriterBottom { get; set; }
        public double TypewriterWidth { get; set; }
        public double TypewriterHeight { get; set; }
        public TypewriterPieceSnapshot[] Pieces { get; set; } = [];
    }

    sealed class TypewriterPieceSnapshot
    {
        public string ClassName { get; set; } = "";
        public double Left { get; set; }
        public double Right { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    sealed class ReducedMotionSnapshot
    {
        public string AriaLabel { get; set; } = "";
        public string WordText { get; set; } = "";
        public string SuffixText { get; set; } = "";
        public string CursorAnimationName { get; set; } = "";
        public double CursorOpacity { get; set; }
    }

    sealed class TrackAlignment
    {
        public double TopDelta { get; set; }
        public double MapHeight { get; set; }
        public double TimelineHeight { get; set; }
    }
}
