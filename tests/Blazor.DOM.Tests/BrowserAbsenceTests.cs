// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Blazor.DOM.Tests;

public sealed class BrowserAbsenceTests
{
    [Fact]
    public void NullAndUndefinedSentinels_UseJsonNullWithoutObjectFallback()
    {
        Assert.Equal("null", JsonSerializer.Serialize(default(BrowserNull)));
        Assert.Equal("null", JsonSerializer.Serialize(default(BrowserUndefined)));
        Assert.Equal(default, JsonSerializer.Deserialize<BrowserNull>("null"));
        Assert.Equal(default, JsonSerializer.Deserialize<BrowserUndefined>("null"));
    }
}
