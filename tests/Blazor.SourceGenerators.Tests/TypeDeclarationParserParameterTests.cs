// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class TypeDeclarationParserParameterTests
{
    [Fact]
    public void SplitTopLevelParameters_EmptyParentheses_ReturnsEmpty()
    {
        var parameters = TypeDeclarationParser.SplitTopLevelParameters("()");

        Assert.Empty(parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_EmptyString_ReturnsEmpty()
    {
        var parameters = TypeDeclarationParser.SplitTopLevelParameters("");

        Assert.Empty(parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_SimpleParameters()
    {
        var parameters = TypeDeclarationParser.SplitTopLevelParameters(
            "(key: string, value: number)");

        Assert.Equal(["key: string", "value: number"], parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_GenericTypeWithCommaIsOneParameter()
    {
        // Regression: previous splitter split on every ',' regardless of generic
        // depth, so 'Map<K, V>' became two tokens and the second parameter slot
        // was filled with the inner type 'V'.
        var parameters = TypeDeclarationParser.SplitTopLevelParameters(
            "(map: Map<string, number>)");

        Assert.Equal(["map: Map<string, number>"], parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_FunctionTypeParameter()
    {
        // Regression: callback parameter signatures like '(e: Event) => void'
        // contain '(' and ')' which the previous splitter stripped indiscriminately.
        var parameters = TypeDeclarationParser.SplitTopLevelParameters(
            "(cb: (e: Event) => void, options?: AddEventListenerOptions)");

        Assert.Equal(["cb: (e: Event) => void", "options?: AddEventListenerOptions"], parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_ObjectLiteralParameter()
    {
        var parameters = TypeDeclarationParser.SplitTopLevelParameters(
            "(opts: { x: number; y: string }, label: string)");

        Assert.Equal(["opts: { x: number; y: string }", "label: string"], parameters);
    }

    [Fact]
    public void SplitTopLevelParameters_NestedGenerics()
    {
        var parameters = TypeDeclarationParser.SplitTopLevelParameters(
            "(values: ReadonlyArray<KeyValue<string, number>>, count: number)");

        Assert.Equal(
            ["values: ReadonlyArray<KeyValue<string, number>>", "count: number"],
            parameters);
    }

    [Fact]
    public void TrySplitNameAndType_SimpleParameter()
    {
        Assert.True(TypeDeclarationParser.TrySplitParameterNameAndType(
            "key: string", out var name, out var type));
        Assert.Equal("key", name);
        Assert.Equal("string", type);
    }

    [Fact]
    public void TrySplitNameAndType_OptionalParameter()
    {
        Assert.True(TypeDeclarationParser.TrySplitParameterNameAndType(
            "options?: AddEventListenerOptions", out var name, out var type));
        Assert.Equal("options?", name);
        Assert.Equal("AddEventListenerOptions", type);
    }

    [Fact]
    public void TrySplitNameAndType_FunctionTypeParameter()
    {
        // Inner ':' inside the function type must not split the name from the
        // overall parameter type.
        Assert.True(TypeDeclarationParser.TrySplitParameterNameAndType(
            "cb: (e: Event) => void", out var name, out var type));
        Assert.Equal("cb", name);
        Assert.Equal("(e: Event) => void", type);
    }

    [Fact]
    public void TrySplitNameAndType_ObjectLiteralParameter()
    {
        Assert.True(TypeDeclarationParser.TrySplitParameterNameAndType(
            "opts: { x: number; y: string }", out var name, out var type));
        Assert.Equal("opts", name);
        Assert.Equal("{ x: number; y: string }", type);
    }

    [Fact]
    public void TrySplitNameAndType_NoColon_ReturnsFalse()
    {
        Assert.False(TypeDeclarationParser.TrySplitParameterNameAndType(
            "rest...", out _, out _));
    }
}
