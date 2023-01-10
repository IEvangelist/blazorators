// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.EndToEndTests;

public sealed partial class LocalStorageTests
{
    private static IEnumerable<object[]> ChromiumStorageInputs
    {
        get
        {
            yield return new object[]
            {
                BrowserType.Chromium, 1, "todo item 1"
            };
            yield return new object[]
            {
                BrowserType.Chromium, 2, "todo item 1", "todo item 2"
            };
            yield return new object[]
            {
                BrowserType.Chromium, 2, "todo item 1", "todo item 2", "todo item 2"
            };
        }
    }
}
