// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using TypeScript.TypeConverter.Parsers;
using Xunit;

namespace TypeScript.TypeConverter.Tests;

public class LibDomParserTests
{
    [Fact]
    public async Task InitializesTypeDefinitionsCorrectly()
    {
        var stopwatch = Stopwatch.StartNew();
        var sut = new LibDomParser();

        await sut.InitializeAsync();
        stopwatch.Stop();

        Assert.True(sut.IsInitialized);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task TryParseDefinitionCorrectly()
    {
        var sut = new LibDomParser();

        await sut.InitializeAsync();

        var expected = @"namespace Microsoft.JSInterop;

public record PositionOptions(
    bool? EnableHighAccuracy,
    double? MaximumAge,
    double? Timeout
);
";

        var result = sut.TryParseType("PositionOptions", out var actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ParseStaticObjectCorrectly()
    {
        var sut = new LibDomParser();

        await sut.InitializeAsync();

        var parserResult = sut.ParseStaticType("Geolocation");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, parserResult.Status);
        Assert.Equal(3, parserResult.Result!.MemberCount);
    }
}
