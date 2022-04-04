// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

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

    public static IEnumerable<object[]> TryGetDeclarationInput
    {
        get
        {
            yield return new object[]
            {
                "PositionCallback",
                @"interface PositionCallback {
    (position: GeolocationPosition): void;
}",
            };

            yield return new object[]
            {
                "PositionOptions",
                @"interface PositionOptions {
    enableHighAccuracy?: boolean;
    maximumAge?: number;
    timeout?: number;
}",
            };
        }
    }

    [
        Theory,
        MemberData(nameof(TryGetDeclarationInput))
    ]
    public void TryGetDeclarationReturnsCorrectly(string typeName, string expected)
    {
        var sut = new LibDomReader();
        var result = sut.TryGetDeclaration(typeName, out var actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(expected.NormalizeNewlines(), actual.NormalizeNewlines());
    }

    public static IEnumerable<object[]> TryGetTypeAliasInput
    {
        get
        {
            yield return new object[]
            {
                "ConnectionType",
                @"type ConnectionType = ""bluetooth"" | ""cellular"" | ""ethernet"" | ""mixed"" | ""none"" | ""other"" | ""unknown"" | ""wifi"";",
            };
        }
    }

    [
        Theory,
        MemberData(nameof(TryGetTypeAliasInput))
    ]
    public void TryGetTypeAliasReturnsCorrectly(string typeAlias, string expected)
    {
        var sut = new LibDomReader();
        var result = sut.TryGetTypeAlias(typeAlias, out var actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(expected.NormalizeNewlines(), actual.NormalizeNewlines());
    }
}
