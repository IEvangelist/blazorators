// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class IndentationTests
{
    [Fact]
    public void Decrease_FromZero_ClampsToZero()
    {
        // Regression: previously `Indentation.Decrease()` underflowed and
        // produced Level=-1. `ToString()` then called
        // `new string(' ', Spaces * Level)` which throws
        // `ArgumentOutOfRangeException` because the count argument is
        // negative. A builder bug elsewhere (an unbalanced
        // increase/decrease) would surface as an opaque generator crash
        // rather than a recoverable indentation issue.
        var indent = new Indentation(0);

        var decreased = indent.Decrease();

        Assert.Equal(0, decreased.Level);
        Assert.Equal("", decreased.ToString());
    }

    [Fact]
    public void Decrease_WithExtra_ClampsAtZero()
    {
        var indent = new Indentation(1);

        var decreased = indent.Decrease(extra: 5);

        Assert.Equal(0, decreased.Level);
        Assert.Equal("", decreased.ToString());
    }

    [Fact]
    public void ResetTo_NegativeValue_ClampsToZero()
    {
        var indent = new Indentation(3);

        var reset = indent.ResetTo(-2);

        Assert.Equal(0, reset.Level);
    }

    [Fact]
    public void Increase_ProducesCorrectIndentation()
    {
        var indent = new Indentation(0);

        var increased = indent.Increase();

        Assert.Equal(1, increased.Level);
        Assert.Equal("    ", increased.ToString());
    }

    [Fact]
    public void ToString_AtLevelTwo_ReturnsEightSpaces()
    {
        var indent = new Indentation(2);

        Assert.Equal("        ", indent.ToString());
    }
}
