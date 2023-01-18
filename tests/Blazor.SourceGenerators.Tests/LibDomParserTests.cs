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
        _sut = new TypeScriptAbstractSyntaxTree(reader.RawSourceText);
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
        var cacheStorage =
            _sut.RootNode.OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
                .Single(c => c is { Identifier: "CacheStorage" });
        Assert.NotNull(cacheStorage);

        var methods = cacheStorage.OfKind(TypeScriptSyntaxKind.MethodSignature);
        Assert.Contains(methods, m => m.Identifier is "has");
        Assert.Contains(methods, m => m.Identifier is "open");
        Assert.Contains(methods, m => m.Identifier is "delete");
        Assert.Contains(methods, m => m.Identifier is "keys");
        Assert.Contains(methods, m => m.Identifier is "match");
    }

    [Fact]
    public void AbstractSyntaxTreeParsesCorrectly()
    {
        var interfaces = _sut.RootNode.OfKind(
            TypeScriptSyntaxKind.InterfaceDeclaration);

        var geolocation = interfaces.Single(
            type => type.Identifier is "Geolocation" &&
            type.Kind is TypeScriptSyntaxKind.InterfaceDeclaration);
        Assert.NotNull(geolocation);
        Assert.Contains(geolocation.Children,
            c => c.Identifier is "getCurrentPosition" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);
        Assert.Contains(geolocation.Children,
            c => c.Identifier is "watchPosition" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);
        Assert.Contains(geolocation.Children,
            c => c.Identifier is "clearWatch" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);

        var watchPosition = geolocation.Children.Single(
            c => c.Identifier is "watchPosition");
        Assert.NotNull(watchPosition);
        Assert.Contains(watchPosition.Children,
            c => c.Identifier is "successCallback" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);
        Assert.Contains(watchPosition.Children,
            c => c.Identifier is "errorCallback" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);
        Assert.Contains(watchPosition.Children,
            c => c.Identifier is "options" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);

        var successCallback = watchPosition.Children.Single(
            c => c.Identifier is "successCallback");
        Assert.NotNull(successCallback);
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
        Assert.Equal(3, methods.Count);
        Assert.Contains(methods, m => m.RawName is "clearWatch");
        Assert.Contains(methods, m => m.RawName is "getCurrentPosition");
        Assert.Contains(methods, m => m.RawName is "watchPosition");

        var properties = result.Properties;
        Assert.NotNull(properties);
        Assert.Empty(properties);

        var dependencies = result.DependentTypes;
        Assert.NotNull(dependencies);
        Assert.Single(dependencies);
        Assert.True(dependencies.ContainsKey("PositionOptions"));
    }
}
