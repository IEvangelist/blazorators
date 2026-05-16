// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for property-level nullability detection.
///
/// Historically <c>ToObject</c> and <c>ToTopLevelObject</c> classified a
/// property as nullable when its type literal <c>Contains("| null")</c>.
/// That returns a false positive for types where <c>| null</c> appears
/// only inside a generic argument or array element (e.g.
/// <c>(number | null)[]</c> or <c>Map&lt;string, string | null&gt;</c>).
/// The property reference itself is non-nullable in those forms; only
/// the inner type alternates.
///
/// The fix tightens detection to require the type to actually end with
/// <c>| null</c>, matching the parameter-side normalization in
/// <see cref="TypeDeclarationParser.ParseParameters"/>.
/// </summary>
public class TypeDeclarationParserPropertyNullabilityTests
{
    [Theory]
    [InlineData(
        @"interface NullableSample {
    plain: number;
}",
        "plain",
        false,
        "non-nullable primitive")]
    [InlineData(
        @"interface NullableSample {
    explicitNull: number | null;
}",
        "explicitNull",
        true,
        "type-level | null")]
    [InlineData(
        @"interface NullableSample {
    nameMarker?: number;
}",
        "nameMarker",
        true,
        "name-level ?")]
    [InlineData(
        @"interface NullableSample {
    both?: number | null;
}",
        "both",
        true,
        "name + type marker")]
    [InlineData(
        @"interface NullableSample {
    arrayOfNullable: (number | null)[];
}",
        "arrayOfNullable",
        false,
        "array of nullable elements; reference itself is non-null")]
    [InlineData(
        @"interface NullableSample {
    mapWithNullableValue: Map<string, string | null>;
}",
        "mapWithNullableValue",
        false,
        "generic with nullable value type; reference itself is non-null")]
    [InlineData(
        @"interface NullableSample {
    nullableArrayOfNullable: (number | null)[] | null;
}",
        "nullableArrayOfNullable",
        true,
        "nullable array whose elements are also nullable")]
    public void Property_IsNullable_ReflectsActualReferenceNullability(
        string typeScriptDeclaration,
        string expectedPropertyName,
        bool expectedIsNullable,
        string description)
    {
        var sut = TypeDeclarationParser.Default;
        var actual = sut.ToObject(typeScriptDeclaration);

        Assert.NotNull(actual);
        Assert.True(
            actual!.Properties.TryGetValue(expectedPropertyName, out var property),
            $"Expected property '{expectedPropertyName}' to be parsed for {description}");
        Assert.True(
            property!.IsNullable == expectedIsNullable,
            $"Expected IsNullable={expectedIsNullable} for {description}, got {property.IsNullable}");
    }
}
