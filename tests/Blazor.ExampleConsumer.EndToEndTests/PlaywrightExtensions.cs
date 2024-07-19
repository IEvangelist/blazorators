// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.EndToEndTests;

internal static class PlaywrightExtensions
{
    internal static IBrowserType ToBrowser(this IPlaywright playwright, BrowserType browser) =>
        browser switch
        {
            BrowserType.Chromium => playwright.Chromium,
            BrowserType.Firefox => playwright.Firefox,
            BrowserType.WebKit => playwright.Webkit,
            _ => throw new ArgumentException($"Unknown browser: {browser}")
        };
}
