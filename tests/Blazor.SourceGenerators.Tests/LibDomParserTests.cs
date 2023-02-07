// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class LibDomParserTests
{
    [Fact]
    public void ParseStaticObjectCorrectly()
    {
        var sut = TypeDeclarationParser.Default;
        var parserResult = sut.ParseTargetType("Geolocation");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, parserResult.Status);

        var result = parserResult.Value;
        Assert.NotNull(result);
        Assert.Equal(3, result.MemberCount);

        var methods = result.Methods;
        Assert.NotNull(methods);
        Assert.Equal(3, methods.Count);
        Assert.Contains(methods, m => m.RawName == "clearWatch");
        Assert.Contains(methods, m => m.RawName == "getCurrentPosition");
        Assert.Contains(methods, m => m.RawName == "watchPosition");

        var properties = result.Properties;
        Assert.NotNull(properties);
        Assert.Empty(properties);

        var dependencies = result.DependentTypes;
        Assert.NotNull(dependencies);
        Assert.Single(dependencies);
        Assert.True(dependencies.ContainsKey("PositionOptions"));
    }
}
