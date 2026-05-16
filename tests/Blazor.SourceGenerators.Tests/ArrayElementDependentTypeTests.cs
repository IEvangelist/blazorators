// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for array-of-custom-type dependent emission.
///
/// When a method parameter or return type is an array of a user-defined
/// TypeScript interface (e.g. <c>MutationRecord[]</c>), the consumer
/// expects the generator to also emit a DTO class for the element
/// type. The previous implementation looked the literal type string
/// up in the declaration map without stripping the array suffix --
/// the lookup missed because no key <c>"MutationRecord[]"</c> exists,
/// only <c>"MutationRecord"</c>. As a result the element DTO never
/// reached the dependent-type emit pipeline and consumers saw
/// generated code referencing an undefined type.
///
/// The fix routes both parameter and return-type lookups through
/// <c>Types.TypeShape</c>, which understands the array shapes
/// (<c>T[]</c>, <c>Array&lt;T&gt;</c>, <c>ReadonlyArray&lt;T&gt;</c>)
/// already used by <c>CSharpProperty</c>.
/// </summary>
public class ArrayElementDependentTypeTests
{
    [Theory]
    [InlineData("MutationRecord[]")]
    [InlineData("Array<MutationRecord>")]
    [InlineData("ReadonlyArray<MutationRecord>")]
    public void Parameter_ArrayOfCustomType_EmitsElementAsDependentDto(string parameterTypeSpelling)
    {
        var dts = @"
interface Observer {
    observe(records: " + parameterTypeSpelling + @"): void;
}
interface MutationRecord {
    type: string;
    target: string;
}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);
        var result = parser.ParseTargetType("Observer");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value!;
        Assert.True(
            topLevel.DependentTypes!.ContainsKey("MutationRecord"),
            $"Expected 'MutationRecord' DTO to be discovered for parameter shape '{parameterTypeSpelling}', but DependentTypes had: {string.Join(", ", topLevel.DependentTypes!.Keys)}");
    }

    [Theory]
    [InlineData("MutationRecord[]")]
    [InlineData("Array<MutationRecord>")]
    [InlineData("ReadonlyArray<MutationRecord>")]
    public void ReturnType_ArrayOfCustomType_EmitsElementAsDependentDto(string returnTypeSpelling)
    {
        var dts = @"
interface Observer {
    takeRecords(): " + returnTypeSpelling + @";
}
interface MutationRecord {
    type: string;
    target: string;
}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);
        var result = parser.ParseTargetType("Observer");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value!;
        var method = Assert.Single(topLevel.Methods!);
        Assert.True(
            method.DependentTypes!.ContainsKey("MutationRecord"),
            $"Expected 'MutationRecord' DTO to be discovered for return shape '{returnTypeSpelling}', but DependentTypes had: {string.Join(", ", method.DependentTypes!.Keys)}");
    }
}
