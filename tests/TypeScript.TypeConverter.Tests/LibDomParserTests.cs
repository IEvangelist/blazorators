// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TypeScript.TypeConverter.Parsers;
using Xunit;

namespace TypeScript.TypeConverter.Tests;

public class LibDomParserTests
{
    [Fact]
    public void ParseStaticObjectCorrectly()
    {
        var sut = new LibDomParser();

        var parserResult = sut.ParseStaticType("Geolocation");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, parserResult.Status);
        Assert.Equal(3, parserResult.Result!.MemberCount);
    }
}
