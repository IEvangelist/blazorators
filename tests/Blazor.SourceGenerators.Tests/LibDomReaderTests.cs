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

        var sut = TypeDeclarationReader.Default;
        _ = sut.TryGetInterface("foo", out var _);

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
                """
                interface PositionCallback {
                    (position: GeolocationPosition): void;
                }
                """,
            };

            yield return new object[]
            {
                "PositionOptions",
                """
                interface PositionOptions {
                    enableHighAccuracy?: boolean;
                    maximumAge?: number;
                    timeout?: number;
                }
                """,
            };
        }
    }

    [
        Theory,
        MemberData(nameof(TryGetDeclarationInput))
    ]
    public void TryGetDeclarationReturnsCorrectly(string typeName, string expected)
    {
        var sut = TypeDeclarationReader.Default;
        var result = sut.TryGetInterface(typeName, out var actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(expected.NormalizeNewlines(), actual.GetText().ToString().Trim().NormalizeNewlines());
    }

    public static IEnumerable<object[]> TryGetTypeAliasInput
    {
        get
        {
            yield return new object[]
            {
                "ClientTypes",
                @"type ClientTypes = ""all"" | ""sharedworker"" | ""window"" | ""worker"";",
            };
        }
    }

    [
        Theory,
        MemberData(nameof(TryGetTypeAliasInput))
    ]
    public void TryGetTypeAliasReturnsCorrectly(string typeAlias, string expected)
    {
        var sut = TypeDeclarationReader.Default;
        var result = sut.TryGetTypeAlias(typeAlias, out var actual);

        Assert.True(result);
        Assert.NotNull(actual);
        Assert.Equal(expected.NormalizeNewlines(), actual.GetText().ToString().Trim().NormalizeNewlines());
    }
}
