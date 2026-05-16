// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for TypeScript primitive alias resolution.
///
/// <para>
/// <c>lib.dom.d.ts</c> declares many trivial aliases that point at a
/// primitive TS type (e.g. <c>type DOMHighResTimeStamp = number;</c>,
/// <c>type GLuint = number;</c>, <c>type GLboolean = boolean;</c>).
/// Previously the parser only resolved string-union aliases
/// (<c>type Direction = "left" | "right";</c>); single-primitive
/// aliases fell through unchanged, so a property typed as
/// <c>DOMHighResTimeStamp</c> emitted the raw TS name into the
/// generated DTO and the method-return path emitted it into the
/// implementation signature -- neither of which compiles.
/// </para>
///
/// <para>
/// These tests pin both surfaces: the property path (where
/// <c>TryGetPrimitiveType</c> hooks in) and the method-return path
/// (which previously skipped alias resolution entirely).
/// </para>
/// </summary>
public class TypeAliasPrimitiveResolutionTests
{
    [Theory]
    [InlineData("DOMHighResTimeStamp", "number", "double")]
    [InlineData("GLuint", "number", "double")]
    [InlineData("GLboolean", "boolean", "bool")]
    [InlineData("CSSOMString", "string", "string")]
    public void Property_TypedAsPrimitiveAlias_EmitsMappedCSharpType(
        string aliasName,
        string aliasRhs,
        string expectedCSharpType)
    {
        var dts = $@"
type {aliasName} = {aliasRhs};
interface Holder {{
    value: {aliasName};
}}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("Holder");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var property = Assert.Single(result.Value!.Properties!);
        Assert.Equal(expectedCSharpType, property.MappedTypeName);
    }

    [Theory]
    [InlineData("DOMHighResTimeStamp", "number")]
    [InlineData("GLuint", "number")]
    [InlineData("GLboolean", "boolean")]
    public void MethodReturnType_AliasToPrimitive_DoesNotRegisterDependentDto(
        string aliasName,
        string aliasRhs)
    {
        // The element type registered as a dependent DTO would be the
        // alias name itself, which has no interface declaration. The
        // expectation is that alias resolution treats it as a primitive
        // and the method is *not* recorded as having a custom DTO
        // dependency.
        var dts = $@"
type {aliasName} = {aliasRhs};
interface Performance {{
    now(): {aliasName};
}}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("Performance");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var method = Assert.Single(result.Value!.Methods!);
        Assert.False(
            method.DependentTypes!.ContainsKey(aliasName),
            $"Expected '{aliasName}' to be resolved as a primitive (not registered as a dependent DTO).");
        Assert.Equal(aliasRhs, method.RawReturnTypeName);
    }

    [Theory]
    [InlineData("DOMHighResTimeStamp", "number")]
    [InlineData("GLuint", "number")]
    [InlineData("GLboolean", "boolean")]
    public void Parameter_AliasToPrimitive_NormalizesToRhsAndSkipsDependentLookup(
        string aliasName,
        string aliasRhs)
    {
        var dts = $@"
type {aliasName} = {aliasRhs};
interface Animator {{
    schedule(time: {aliasName}): void;
}}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("Animator");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value!;
        Assert.False(
            topLevel.DependentTypes!.ContainsKey(aliasName),
            $"Expected '{aliasName}' to be resolved as a primitive (not registered as a dependent DTO).");

        var method = Assert.Single(topLevel.Methods!);
        var param = Assert.Single(method.ParameterDefinitions);
        Assert.Equal(aliasRhs, param.RawTypeName);
    }
}
