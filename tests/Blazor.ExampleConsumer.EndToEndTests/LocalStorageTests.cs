// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.EndToEndTests;

[Collection(ExampleSiteCollection.Name)]
[Trait("Category", "EndToEnd")]
public sealed partial class LocalStorageTests(BlazoratorsSiteFixture site)
{
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
        await page.GotoAsync(site.UrlFor("/todos"));
        await page.EvaluateAsync("() => localStorage.clear()");
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Act
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
    internal const string TodoInput = "#todo";

    internal const string AddButton = "#add";

    internal const string TodoList = "#todo-list > li";
}