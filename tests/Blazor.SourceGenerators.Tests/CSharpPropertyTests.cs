// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class CSharpPropertyTests
{
    [Theory]
    [InlineData("string[]")]
    [InlineData("ReadonlyArray<string>")]
    [InlineData("Array<string>")]
    public void IsArray_TrueForAllArrayForms(string rawType)
    {
        var property = new CSharpProperty("items", rawType);

        Assert.True(property.IsArray);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("double")]
    [InlineData("MyType")]
    [InlineData("Map<string, string>")]
    public void IsArray_FalseForNonArrayTypes(string rawType)
    {
        var property = new CSharpProperty("value", rawType);

        Assert.False(property.IsArray);
    }

    [Theory]
    [InlineData("string[]", "string")]
    [InlineData("ReadonlyArray<string>", "string")]
    [InlineData("Array<string>", "string")]
    [InlineData("number[]", "double")]
    [InlineData("ReadonlyArray<number>", "double")]
    [InlineData("Array<number>", "double")]
    [InlineData("FontFace[]", "FontFace")]
    [InlineData("ReadonlyArray<FontFace>", "FontFace")]
    public void MappedTypeName_ReturnsElementType_ForArrayForms(string rawType, string expected)
    {
        var property = new CSharpProperty("items", rawType);

        Assert.Equal(expected, property.MappedTypeName);
    }

    [Fact]
    public void MappedTypeName_DoesNotAggressivelyStripBrackets_FromNonArrayGenerics()
    {
        // Regression: previous implementation called Replace("[]", "") indiscriminately.
        // A non-array type whose name happens to contain "[]" (impossible in well-formed
        // TS but a guardrail nonetheless) or a generic type with embedded array element
        // type should not be silently mangled.
        var property = new CSharpProperty("value", "Map<string, string>");

        Assert.False(property.IsArray);
        Assert.Equal("Map<string, string>", property.MappedTypeName);
    }

    [Theory]
    [InlineData("string[]", "string[]")]
    [InlineData("ReadonlyArray<string>", "string[]")]
    [InlineData("Array<string>", "string[]")]
    [InlineData("number[]", "double[]")]
    [InlineData("FontFace[]", "FontFace[]")]
    public void GetPropertyTypes_PreservesArraySuffix_OnWasm(string rawType, string expected)
    {
        // Regression: `MappedTypeName` returns the element type, so the
        // property-emit helper must re-attach `[]` when `IsArray` is true.
        // Without this, top-level interface properties with array-shaped
        // TypeScript types lost the `[]` suffix.
        var property = new CSharpProperty("items", rawType);
        var options = new GeneratorOptions(SupportsGenerics: false, IsWebAssembly: true);

        var (returnType, bareType) = property.GetPropertyTypes(options);

        Assert.Equal(expected, bareType);
        Assert.Equal(expected, returnType);
    }

    [Fact]
    public void GetPropertyTypes_PreservesArraySuffix_OnServer()
    {
        var property = new CSharpProperty("items", "string[]");
        var options = new GeneratorOptions(SupportsGenerics: false, IsWebAssembly: false);

        var (returnType, bareType) = property.GetPropertyTypes(options);

        Assert.Equal("string[]", bareType);
        Assert.Equal("ValueTask<string[]>", returnType);
    }

    [Theory]
    // Regression: previously `IsArray` checked `EndsWith("[]")` against the
    // raw TS type. For `T[] | null` (or the Array<T> / ReadonlyArray<T>
    // variants paired with `| null`), `IsArray` returned `false` and the
    // emitter took a fallback path that left the TS primitive name in
    // place. The bug was hidden in bundled `lib.dom.d.ts` because no
    // top-level interface property uses that exact shape, but any custom
    // `.d.ts` shipping `readonly tags: string[] | null` would have been
    // emitted as `public string[]` (no nullable marker) on the interface
    // and worse, with the TS primitive name preserved on dependent DTOs.
    [InlineData("string[] | null")]
    [InlineData("ReadonlyArray<string> | null")]
    [InlineData("Array<string> | null")]
    public void IsArray_TrueForNullableArrayForms(string rawType)
    {
        var property = new CSharpProperty("items", rawType, IsNullable: true);

        Assert.True(property.IsArray);
    }

    [Theory]
    [InlineData("string[] | null", "string")]
    [InlineData("ReadonlyArray<string> | null", "string")]
    [InlineData("Array<string> | null", "string")]
    // Primitive-mapping must apply to the array element after the
    // nullable clause is stripped - `number` is the TS spelling, `double`
    // is the C# mapping.
    [InlineData("number[] | null", "double")]
    [InlineData("ReadonlyArray<number> | null", "double")]
    [InlineData("Array<number> | null", "double")]
    public void MappedTypeName_ReturnsElementType_ForNullableArrayForms(string rawType, string expected)
    {
        var property = new CSharpProperty("items", rawType, IsNullable: true);

        Assert.Equal(expected, property.MappedTypeName);
    }

    [Theory]
    [InlineData("string[] | null", "string[]?")]
    [InlineData("number[] | null", "double[]?")]
    [InlineData("ReadonlyArray<string> | null", "string[]?")]
    [InlineData("Array<string> | null", "string[]?")]
    public void GetPropertyTypes_NullableArrayPreservesElementMappingOnWasm(
        string rawType, string expectedBareType)
    {
        var property = new CSharpProperty("items", rawType, IsNullable: true);
        var options = new GeneratorOptions(SupportsGenerics: false, IsWebAssembly: true);

        var (returnType, bareType) = property.GetPropertyTypes(options);

        Assert.Equal(expectedBareType, bareType);
        Assert.Equal(expectedBareType, returnType);
    }
}
