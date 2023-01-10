// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.EndToEndTests;

[Trait("Category", "EndToEnd")]
public sealed partial class LocalStorageTests
{
    const string DemoSite = "https://ievangelist.github.io/blazorators";

    static bool IsDebugging => Debugger.IsAttached;
    static bool IsHeadless => !IsDebugging;

    public static IEnumerable<object[]> AllStorageTestInputs =>
        ChromiumStorageInputs.Concat(FirefoxStorageInputs);

    [
        Theory,
        MemberData(nameof(AllStorageTestInputs))
    ]
    public async Task LocalStorageCorrectlyReadsAndWritesTodos(
        BrowserType browserType,
        int expectedTodoCount,
        params string[] todos)
    {
        // Arrange
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.ToBrowser(browserType)
            .LaunchAsync(new() { Headless = IsHeadless });

        await using var context = await browser.NewContextAsync();

        var page = await context.NewPageAsync();
        if (IsDebugging)
        {
            page.SetDefaultTimeout(0);
        }
        await page.GotoAsync(DemoSite);

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
    internal const string StoragePagetNavLink = // "#storage";
        "#app > div > div > div.collapse > nav > div:nth-child(2) > a";

    internal const string ClearAllButton = // "#clearall";
        "#app > div > main > article > div:nth-child(3) > div > div > div > div > button.btn.btn-danger";

    internal const string TodoInput = // "#todo";
        "#app > div > main > article > div:nth-child(3) > div > div > div > div > input";

    internal const string AddButton = // "#add";
        "#app > div > main > article > div:nth-child(3) > div > div > div > div > button.btn.btn-primary";
    
    internal const string TodoList = // "#todo-list > li"
        "#app > div > main > article > div:nth-child(3) > div > ul > li";
}