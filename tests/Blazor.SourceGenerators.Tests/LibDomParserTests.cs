// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class LibDomParserTests
{
    private readonly ITypeScriptAbstractSyntaxTree _sut;

    public LibDomParserTests()
    {
        var reader = TypeDeclarationReader.Default;
        _sut = TypeScriptAbstractSyntaxTree.FromSourceText(reader.RawSourceText);
    }

    [Fact]
    public void FindsInterfaceAndCorrespondingImplementationCorrectly()
    {
        var window = _sut.RootNode.Window;
        Assert.NotNull(window);

        var heritage = window.Children.Single(
            c => c.Kind is TypeScriptSyntaxKind.HeritageClause);
        Assert.NotNull(heritage);
        Assert.Contains(heritage.Children, c => c.Identifier is "WindowOrWorkerGlobalScope");
    }

    [Fact]
    public void CanReplaceBruteForceParser()
    {
        var cacheStorage = _sut.RootNode.OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
            .Single(type => type is { Identifier: "CacheStorage" });

        Assert.NotNull(cacheStorage);

        var methods = cacheStorage.OfKind(TypeScriptSyntaxKind.MethodSignature);
        Assert.Collection(methods,
            method => Assert.Equal("delete", method.Identifier),
            method => Assert.Equal("has", method.Identifier),
            method => Assert.Equal("keys", method.Identifier),
            method => Assert.Equal("match", method.Identifier),
            method => Assert.Equal("open", method.Identifier));
    }

    [Fact]
    public void AbstractSyntaxTreeParsesCorrectly()
    {
        var interfaces = _sut.RootNode.OfKind(
            TypeScriptSyntaxKind.InterfaceDeclaration);

        var geolocation = interfaces.Single(
            type => type is { Identifier: "Geolocation", Kind: TypeScriptSyntaxKind.InterfaceDeclaration });

        Assert.NotNull(geolocation);

        var methods = geolocation.OfKind(TypeScriptSyntaxKind.MethodSignature);
        Assert.Collection(methods,
            method => Assert.Equal("clearWatch", method.Identifier),
            method => Assert.Equal("getCurrentPosition", method.Identifier),
            method => Assert.Equal("watchPosition", method.Identifier));

        var watchPosition = geolocation.Children.Single(
            c => c.Identifier is "watchPosition");
        Assert.NotNull(watchPosition);

        var parameters = watchPosition.OfKind(TypeScriptSyntaxKind.Parameter);
        Assert.Collection(parameters,
           parameter => Assert.Equal("successCallback", parameter.Identifier),
           parameter => Assert.Equal("errorCallback", parameter.Identifier),
           parameter => Assert.Equal("options", parameter.Identifier));

        var successCallback = Assert.IsType<ParameterDeclaration>(
            watchPosition.Children.Single(c => c.Identifier is "successCallback"));
        Assert.Equal("PositionCallback", successCallback.Type.GetText().ToString().Trim());
    }

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
        Assert.Collection(methods,
            method => Assert.Equal("clearWatch", method.RawName),
            method => Assert.Equal("getCurrentPosition", method.RawName),
            method => Assert.Equal("watchPosition", method.RawName));

        var properties = result.Properties;
        Assert.NotNull(properties);
        Assert.Empty(properties);

        var dependencies = result.DependentTypes;
        Assert.NotNull(dependencies);
        Assert.Contains("PositionOptions", dependencies);
    }

    [Fact]
    public void VerifyLocalStorageCanBeReadByDefault()
    {
        var sut = TypeDeclarationParser.Default;
        var parserResult = sut.ParseTargetType("Storage");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, parserResult.Status);

        // Assert
        var properties = parserResult.Value?.Properties;
        Assert.NotNull(properties);
        Assert.Collection(properties,
            property => Assert.Equal("length", property.RawName));

        var methods = parserResult.Value?.Methods;
        Assert.NotNull(methods);
        Assert.Collection(methods,
            method => Assert.Equal("clear", method.RawName),
            method => Assert.Equal("getItem", method.RawName),
            method => Assert.Equal("key", method.RawName),
            method => Assert.Equal("removeItem", method.RawName),
            method => Assert.Equal("setItem", method.RawName));
    }
}