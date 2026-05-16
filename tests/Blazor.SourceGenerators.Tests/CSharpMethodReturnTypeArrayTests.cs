// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for <see cref="CSharpMethodExtensions.GetMethodTypes"/>.
///
/// Previously the return-type resolver only ran the primitive map
/// against the full <see cref="CSharpMethod.RawReturnTypeName"/>. For
/// scalar primitives this works (<c>number</c> -> <c>double</c>), but
/// TS array primitives such as <c>number[]</c> were left untouched
/// and the emitter dropped <c>number[]</c> straight into the
/// generated method signature and <c>_javaScript.Invoke&lt;T&gt;()</c>
/// call -- both invalid C#.
///
/// The fix mirrors the array-element resolution in
/// <c>CSharpProperty.MappedTypeName</c>: if the bare return type is
/// array-shaped (<c>T[]</c>, <c>Array&lt;T&gt;</c>,
/// <c>ReadonlyArray&lt;T&gt;</c>) with a primitive element, map the
/// element and re-attach <c>[]</c>.
/// </summary>
public class CSharpMethodReturnTypeArrayTests
{
    private static GeneratorOptions WebAssembly => new(SupportsGenerics: false, IsWebAssembly: true);
    private static GeneratorOptions Server => new(SupportsGenerics: false, IsWebAssembly: false);

    private static CSharpMethod MakeMethod(string returnType) =>
        new("doStuff", returnType, [], null);

    [Theory]
    [InlineData("number[]", "double[]")]
    [InlineData("boolean[]", "bool[]")]
    [InlineData("Date[]", "DateTime[]")]
    [InlineData("DOMTimeStamp[]", "long[]")]
    [InlineData("Array<number>", "double[]")]
    [InlineData("ReadonlyArray<number>", "double[]")]
    public void GetMethodTypes_WebAssemblyArrayPrimitive_MapsElement(
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
    [InlineData("number[]", "ValueTask<double[]>", "double[]")]
    [InlineData("boolean[]", "ValueTask<bool[]>", "bool[]")]
    [InlineData("Date[]", "ValueTask<DateTime[]>", "DateTime[]")]
    public void GetMethodTypes_ServerArrayPrimitive_WrapsInValueTask(
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

    [Fact]
    public void GetMethodTypes_NonPrimitiveArray_LeavesRawElementName()
    {
        // Array of a user-defined dependent type (DTO) emits the raw
        // element name -- the DTO is emitted as a sibling .g.cs file
        // with exactly that name.
        var (returnType, bareType) = MakeMethod("SomeRecord[]").GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal("SomeRecord[]", returnType);
        Assert.Equal("SomeRecord[]", bareType);
    }

    [Fact]
    public void GetMethodTypes_VoidReturnType_StillEmitsVoid()
    {
        var (returnType, _) = MakeMethod("void").GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: true);

        Assert.Equal("void", returnType);
    }
}
