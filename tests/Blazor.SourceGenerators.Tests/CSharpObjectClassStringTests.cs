// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class CSharpObjectClassStringTests
{
    [Fact]
    public void ToClassString_UsesLineFeedNewLinesOnly()
    {
        // Regression: dependent DTO class output used '\r\n' while the rest of the
        // generator pipeline emits '\n'. Mixed line endings produced inconsistent
        // diffs and tripped up snapshot-style consumers.
        var obj = new CSharpObject("MyType", null);
        obj.Properties.Add("value", new CSharpProperty("value", "string"));

        var output = obj.ToString();

        Assert.DoesNotContain("\r\n", output);
        Assert.Contains("\n", output);
    }

    [Fact]
    public void ToClassString_DoesNotContainBareCarriageReturns()
    {
        var obj = new CSharpObject("MyType", null);
        obj.Properties.Add("value", new CSharpProperty("value", "string"));

        var output = obj.ToString();

        Assert.DoesNotContain('\r', output);
    }

    [Fact]
    public void ToClassString_IncludesEpochTimeStampHelperWithLineFeeds()
    {
        // The DOMTimeStamp / EpochTimeStamp branch is the most line-feed-heavy
        // path; assert it stays LF-only.
        var obj = new CSharpObject("Event", null);
        obj.Properties.Add("timeStamp", new CSharpProperty("timeStamp", "EpochTimeStamp"));

        var output = obj.ToString();

        Assert.DoesNotContain("\r\n", output);
        Assert.Contains("TimeStampAsUtcDateTime", output);
    }
}
