// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for handling the TypeScript <c>| undefined</c>
/// clause in return types, parameters, and properties.
///
/// <para>
/// The DOM uses <c>| undefined</c> as a semantically-equivalent
/// counterpart to <c>| null</c> for optional/missing values. Real
/// examples returned from <c>Promise&lt;T&gt;</c>:
/// </para>
///
/// <list type="bullet">
///   <item><c>Cache.match(): Promise&lt;Response | undefined&gt;</c></item>
///   <item><c>ServiceWorkerContainer.getRegistration(): Promise&lt;ServiceWorkerRegistration | undefined&gt;</c></item>
/// </list>
///
/// <para>
/// Before the fix the generator only recognised <c>| null</c> and left
/// <c>| undefined</c> in place, emitting invalid C# tokens like
/// <c>Response | undefined</c> in method signatures and on
/// <c>Invoke&lt;...&gt;()</c> call sites. Both forms now collapse to
/// the C# nullable suffix (<c>T?</c>) -- the runtime distinction
/// between <c>null</c> and <c>undefined</c> isn't observable from the
/// Blazor / System.Text.Json side, so a single shape is sufficient.
/// </para>
/// </summary>
public class TypeShapeUndefinedClauseTests
{
    [Theory]
    [InlineData("string | null", "string")]
    [InlineData("string | undefined", "string")]
    [InlineData("Response | undefined", "Response")]
    [InlineData("PushSubscription | null", "PushSubscription")]
    [InlineData("string[] | null", "string[]")]
    [InlineData("string[] | undefined", "string[]")]
    [InlineData("Array<Entry> | undefined", "Array<Entry>")]
    public void StripNullClause_BothFormsAreStripped(string input, string expected)
    {
        Assert.Equal(expected, Types.TypeShape.StripNullClause(input));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("Response")]
    [InlineData("number[]")]
    [InlineData("Array<Entry>")]
    [InlineData("any")]
    public void StripNullClause_NonNullableInput_RoundTrips(string input)
    {
        Assert.Equal(input, Types.TypeShape.StripNullClause(input));
    }

    [Theory]
    [InlineData("string[] | null")]
    [InlineData("string[] | undefined")]
    [InlineData("Array<Entry> | undefined")]
    [InlineData("ReadonlyArray<string> | null")]
    public void TryGetArrayElementTypeName_NullableArray_ReturnsElement(string input)
    {
        var ok = Types.TypeShape.TryGetArrayElementTypeName(input, out var element);
        Assert.True(ok, $"Expected `{input}` to match an array shape after stripping the null/undefined clause.");
        Assert.NotEqual(string.Empty, element);
    }

    [Fact]
    public void CSharpMethod_IsReturnTypeNullable_UndefinedSuffix_IsNullable()
    {
        // `Cache.match(): Promise<Response | undefined>` --
        // the unwrapped return is `Response | undefined`. After
        // peeling the Promise wrapper the downstream pipeline needs
        // to recognise this as nullable so the emitted method
        // returns `ValueTask<Response?>` instead of `ValueTask<Response | undefined>`.
        var method = new CSharpMethod(
            RawName: "Match",
            RawReturnTypeName: "Response | undefined",
            ParameterDefinitions: []);

        Assert.True(method.IsReturnTypeNullable);
    }

    [Fact]
    public void CSharpMethod_IsReturnTypeNullable_NullSuffix_StillNullable()
    {
        // Sanity regression -- `| null` must keep working.
        var method = new CSharpMethod(
            RawName: "GetSubscription",
            RawReturnTypeName: "PushSubscription | null",
            ParameterDefinitions: []);

        Assert.True(method.IsReturnTypeNullable);
    }

    [Fact]
    public void CSharpMethod_IsReturnTypeNullable_NoNullClause_NotNullable()
    {
        var method = new CSharpMethod(
            RawName: "QueryPermission",
            RawReturnTypeName: "PermissionStatus",
            ParameterDefinitions: []);

        Assert.False(method.IsReturnTypeNullable);
    }

    [Fact]
    public void CSharpProperty_MappedTypeName_UndefinedSuffix_StripsClause()
    {
        // TS property declaration like `foo?: Bar | undefined` (or
        // bare `foo: Bar | undefined`). The mapped C# spelling must
        // strip the clause so the emitted property type compiles
        // (the `?` nullability is appended by the surrounding emitter
        // based on `IsNullable`).
        var property = new CSharpProperty(
            RawName: "registration",
            RawTypeName: "ServiceWorkerRegistration | undefined",
            IsNullable: true);

        Assert.Equal("ServiceWorkerRegistration", property.MappedTypeName);
    }
}
