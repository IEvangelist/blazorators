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

    [Theory]
    // Plain primitive paired with ` | null` on the type (not on the name).
    [InlineData(
        "interface IFoo {\n    bar(name: string | null): void;\n}",
        "name",
        "string?")]
    // Array primitive paired with ` | null` (the original failure case).
    [InlineData(
        "interface IFoo {\n    bar(items: string[] | null): void;\n}",
        "items",
        "string[]?")]
    // Array of custom interface paired with ` | null`. The parser should
    // still emit a clean nullable C# array even when the element type is
    // not a TS primitive.
    [InlineData(
        "interface IFoo {\n    bar(records: SomeRecord[] | null): void;\n}\ninterface SomeRecord {\n    value: number;\n}",
        "records",
        "SomeRecord[]?")]
    public void ParseParameters_TypeLevelNullClauseEmitsNullableCSharp(
        string typeScriptDeclarations,
        string expectedParameterName,
        string expectedTypeSuffix)
    {
        // Regression: when a parameter was declared as `x: T | null` (with
        // the `| null` on the type rather than `?` on the name), the parser
        // left the literal "| null" in the resulting C# type. The output
        // emitted `string[] | null items` -- invalid C#. Normalize the
        // `| null` clause to `isNullable=true` and a clean element type.
        var reader = new Readers.TypeDeclarationReader(typeScriptDeclarations);
        var parser = new TypeDeclarationParser(reader);
        var result = parser.ParseTargetType("IFoo");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value;
        Assert.NotNull(topLevel);

        var method = Assert.Single(topLevel!.Methods!);
        var parameter = Assert.Single(method.ParameterDefinitions);

        Assert.Equal(expectedParameterName, parameter.RawName);
        Assert.True(parameter.IsNullable);

        // `ToParameterString(isGenericType: false)` is what the emitter
        // calls when building the interface/impl signatures. The previous
        // buggy code path would produce `string[] | null items` here.
        var emitted = parameter.ToParameterString();
        Assert.Contains(expectedTypeSuffix, emitted);
        Assert.DoesNotContain("| null", emitted);
    }
}
