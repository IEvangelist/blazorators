// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.EndToEndTests;

[Trait("Category", "EndToEnd")]
public sealed partial class LocalStorageTests
{
    public static readonly TheoryData<BrowserType, int, string[]> ChromiumStorageInputs = new()
    {
        { BrowserType.Chromium, 1, new[] { "todo item 1" } },
        { BrowserType.Chromium, 2, new[] { "todo item 1", "todo item 2" } },
        { BrowserType.Chromium, 3, new[] { "todo item 1", "todo item 2", "todo item 3" } }
    };

    public static readonly TheoryData<BrowserType, int, string[]> FirefoxStorageInputs = new()
    {
        { BrowserType.Firefox, 1, new[] { "todo item 1" } },
        { BrowserType.Firefox, 2, new[] { "todo item 1", "todo item 2" } },
        { BrowserType.Firefox, 3, new[] { "todo item 1", "todo item 2", "todo item 3" } }
    };

    public static readonly TheoryData<BrowserType, int, string[]> WebKitStorageInputs = new()
    {
        { BrowserType.WebKit, 1, new[] { "todo item 1" } },
        { BrowserType.WebKit, 2, new[] { "todo item 1", "todo item 2" } },
        { BrowserType.WebKit, 3, new[] { "todo item 1", "todo item 2", "todo item 3" } }
    };

    private const string DemoSite = "https://ievangelist.github.io/blazorators";

    private static bool IsDebugging => Debugger.IsAttached;
    private static bool IsHeadless => !IsDebugging;

    [Theory]
    [MemberData(nameof(ChromiumStorageInputs))]
    [MemberData(nameof(FirefoxStorageInputs))]
    //[MemberData(nameof(WebKitStorageInputs))] WebKit timeout exceed
    public async Task LocalStorageCorrectlyReadsAndWritesTodos(BrowserType browserType, int expectedTodoCount, params string[] todos)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.ToBrowser(browserType).LaunchAsync(new BrowserTypeLaunchOptions { Headless = IsHeadless });

        // Arrange
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page!.GotoAsync(DemoSite);

        // Act
        await page.ClickAsync(Selectors.StoragePagetNavLink);
        await page.ClickAsync(Selectors.ClearAllButton);

        foreach (var todo in todos)
        {
            await page.FillAsync(Selectors.TodoInput, todo);
            await page.ClickAsync(Selectors.AddButton);
        }

        // Assert
        var locator = page.Locator(Selectors.TodoList);
        await Assertions.Expect(locator).ToHaveCountAsync(expectedTodoCount);
    }
}

file static class Selectors
{
    internal const string AddButton = "#add";
    internal const string ClearAllButton = "#clearall";
    internal const string StoragePagetNavLink = "#storage";
    internal const string TodoInput = "#todo";
    internal const string TodoList = "#todo-list > li";
}