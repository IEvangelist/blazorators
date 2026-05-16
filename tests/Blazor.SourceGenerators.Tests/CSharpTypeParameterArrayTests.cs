// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for <see cref="CSharpType.ToParameterString"/>'s
/// handling of array-typed parameters.
///
/// The previous implementation only ran <c>TypeMap.PrimitiveTypes[..]</c>
/// against the full <see cref="CSharpType.RawTypeName"/>. That matches
/// for scalar primitives (`number`, `string`, `boolean`) but misses
/// array forms (`number[]`, `boolean[]`, `Date[]`) because the map
/// doesn't store the array spelling. As a result the emitter produced
/// raw TypeScript spellings like <c>number[] segments</c> -- which is
/// not valid C#. Map any array shape by mapping its element type and
/// re-attaching <c>[]</c> so callers see <c>double[] segments</c>.
/// </summary>
public class CSharpTypeParameterArrayTests
{
    [Theory]
    [InlineData("number", "double[] values")]
    [InlineData("boolean", "bool[] values")]
    [InlineData("Date", "DateTime[] values")]
    [InlineData("DOMTimeStamp", "long[] values")]
    [InlineData("string", "string[] values")]
    public void ToParameterString_TypeScriptArrayPrimitive_MapsToCSharpArray(
        string elementType,
        string expected)
    {
        var raw = $"{elementType}[]";
        var parameter = new CSharpType("values", raw);

        Assert.Equal(expected, parameter.ToParameterString());
    }

    [Theory]
    [InlineData("number", "double[]? values = null")]
    [InlineData("boolean", "bool[]? values = null")]
    public void ToParameterString_NullableTypeScriptArrayPrimitive_MapsToCSharpNullableArray(
        string elementType,
        string expected)
    {
        var raw = $"{elementType}[]";
        var parameter = new CSharpType("values", raw, IsNullable: true);

        Assert.Equal(expected, parameter.ToParameterString());
    }

    [Fact]
    public void ToParameterString_NonPrimitiveArray_LeavesElementTypeUntouched()
    {
        // A user-defined dependent type used as an array parameter must
        // emit its raw element name (the DTO is emitted as a sibling
        // file with that exact name).
        var parameter = new CSharpType("records", "SomeRecord[]");
        Assert.Equal("SomeRecord[] records", parameter.ToParameterString());
    }
}
