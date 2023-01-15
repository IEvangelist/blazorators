// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class LibDomParserTests
{
    [Fact]
    public void AbstractSyntaxTreeParsesCorrectly()
    {
        var reader = TypeDeclarationReader.Default;
        Assert.True(reader.TryGetDeclaration(
            "Geolocation", out var declaration));

        //var startingTimestamp = Stopwatch.GetTimestamp();

        var astParser =
            new TypeScriptAbstractSyntaxTree(reader.RawSourceText, setChildren: true);

        //var elapsedTime = Stopwatch.GetElapsedTime(startingTimestamp);

        var interfaces = astParser.RootNode.OfKind(
            TypeScriptSyntaxKind.InterfaceDeclaration);

        var geolocation = interfaces.Single(
            type => type.IdentifierStr is "Geolocation" &&
            type.Kind is TypeScriptSyntaxKind.InterfaceDeclaration);
        Assert.NotNull(geolocation);
        Assert.Contains(geolocation.Children,
            c => c.IdentifierStr is "getCurrentPosition" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);
        Assert.Contains(geolocation.Children,
            c => c.IdentifierStr is "watchPosition" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);
        Assert.Contains(geolocation.Children,
            c => c.IdentifierStr is "clearWatch" &&
            c.Kind is TypeScriptSyntaxKind.MethodSignature);

        var watchPosition = geolocation.Children.Single(
            c => c.IdentifierStr is "watchPosition");
        Assert.NotNull(watchPosition);
        Assert.Contains(watchPosition.Children,
            c => c.IdentifierStr is "successCallback" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);
        Assert.Contains(watchPosition.Children,
            c => c.IdentifierStr is "errorCallback" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);
        Assert.Contains(watchPosition.Children,
            c => c.IdentifierStr is "options" &&
            c.Kind is TypeScriptSyntaxKind.Parameter);
        
        var successCallback = watchPosition.Children.Single(
            c => c.IdentifierStr is "successCallback");
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
