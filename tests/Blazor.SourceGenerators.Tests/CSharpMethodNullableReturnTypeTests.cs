// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for <see cref="CSharpMethodExtensions.GetMethodTypes"/>
/// when a method's return type carries a trailing TypeScript
/// <c>| null</c> clause.
///
/// <para>
/// Primitive nullable returns (<c>string | null</c>, <c>number | null</c>,
/// <c>boolean | null</c>) are covered by direct entries in
/// <c>TypeMap.PrimitiveTypes</c>, so they emit C# nullable spellings
/// (<c>string?</c>, <c>double?</c>, etc.).
/// </para>
///
/// <para>
/// Previously a method whose return type was a *custom* type with a
/// trailing <c>| null</c> clause (e.g. <c>getNode(): Node | null;</c>,
/// <c>getActiveElement(): Element | null;</c>, or
/// <c>getElementById(id: string): HTMLElement | null;</c>) dropped the
/// raw TypeScript spelling straight into the generated C# signature
/// and the <c>_javaScript.Invoke&lt;T&gt;()</c> call -- both invalid
/// C# (<c>Node | null</c> is not legal C# syntax). The DOM has dozens
/// of methods in this shape, so the generator was effectively unable
/// to target any interface whose surface returned a nullable element
/// reference.
/// </para>
///
/// <para>
/// The fix peels the trailing <c>| null</c>, runs the primitive /
/// array-element resolution against the bare type, and re-attaches
/// <c>?</c> in C# (so the same code path that handles primitive
/// nullables also handles custom-type nullables and array-of-T
/// nullables).
/// </para>
/// </summary>
public class CSharpMethodNullableReturnTypeTests
{
    private static GeneratorOptions WebAssembly => new(SupportsGenerics: false, IsWebAssembly: true);
    private static GeneratorOptions Server => new(SupportsGenerics: false, IsWebAssembly: false);

    private static CSharpMethod MakeMethod(string returnType) =>
        new("doStuff", returnType, [], null);

    [Theory]
    [InlineData("Node | null", "Node?")]
    [InlineData("Element | null", "Element?")]
    [InlineData("HTMLElement | null", "HTMLElement?")]
    public void GetMethodTypes_WebAssemblyCustomNullable_EmitsCSharpNullable(
        string rawReturnType,
        string expected)
    {
        var (returnType, bareType) = MakeMethod(rawReturnType).GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal(expected, returnType);
        Assert.Equal(expected, bareType);
    }

    [Theory]
    [InlineData("Node | null", "ValueTask<Node?>", "Node?")]
    [InlineData("HTMLElement | null", "ValueTask<HTMLElement?>", "HTMLElement?")]
    public void GetMethodTypes_ServerCustomNullable_WrapsInValueTaskWithNullable(
        string rawReturnType,
        string expectedReturnType,
        string expectedBareType)
    {
        var (returnType, bareType) = MakeMethod(rawReturnType).GetMethodTypes(
            Server,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal(expectedReturnType, returnType);
        Assert.Equal(expectedBareType, bareType);
    }

    [Theory]
    [InlineData("Node[] | null", "Node[]?")]
    [InlineData("Element[] | null", "Element[]?")]
    public void GetMethodTypes_WebAssemblyCustomArrayNullable_EmitsCSharpNullable(
        string rawReturnType,
        string expected)
    {
        var (returnType, bareType) = MakeMethod(rawReturnType).GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal(expected, returnType);
        Assert.Equal(expected, bareType);
    }

    [Theory]
    [InlineData("number[] | null", "double[]?")]
    [InlineData("boolean[] | null", "bool[]?")]
    public void GetMethodTypes_WebAssemblyPrimitiveArrayNullable_MapsElement(
        string rawReturnType,
        string expected)
    {
        var (returnType, bareType) = MakeMethod(rawReturnType).GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal(expected, returnType);
        Assert.Equal(expected, bareType);
    }

    [Fact]
    public void GetMethodTypes_PrimitiveScalarNullable_UnchangedByFix()
    {
        // Sanity check that the existing primitive-nullable path
        // (driven by direct map entries like "string | null" -> "string?")
        // is not regressed by the new custom-type-nullable handling.
        var (returnType, bareType) = MakeMethod("string | null").GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: true);

        Assert.Equal("string?", returnType);
        Assert.Equal("string?", bareType);
    }
}
