// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using TypeScript.TypeConverter.Readers;
using Xunit;

namespace TypeScript.TypeConverter.Tests;

public class LibDomReaderTests
{
    [Fact]
    public void InitializesTypeDefinitionsCorrectly()
    {
        var stopwatch = Stopwatch.StartNew();
        var sut = new LibDomReader();

        _ = sut.TryGetDeclaration("foo", out var _);
        stopwatch.Stop();

        Assert.True(sut.IsInitialized);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void TryParseDefinitionCorrectly()
    {
        var sut = new LibDomReader();

        var expected = @"interface PositionOptions {
    enableHighAccuracy?: boolean;
    maximumAge?: number;
    timeout?: number;
}";

        var result = sut.TryGetDeclaration("PositionOptions", out var actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(expected.NormalizeNewlines(), actual.NormalizeNewlines());
    }
}
