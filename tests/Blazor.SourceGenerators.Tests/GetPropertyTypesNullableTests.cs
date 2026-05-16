// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for nullable annotations on the top-level
/// interface / implementation property emission path. The DTO emit
/// path (<see cref="Blazor.SourceGenerators.CSharp.CSharpObject"/>)
/// already appends a <c>?</c> when
/// <see cref="CSharpProperty.IsNullable"/> is set and the mapped
/// type doesn't already end with <c>?</c>; the parallel top-level
/// path
/// (<see cref="Blazor.SourceGenerators.Extensions.CSharpPropertyExtensions.GetPropertyTypes(CSharpProperty, GeneratorOptions)"/>)
/// must keep the same invariant. A regression here would emit
/// <c>NavigationHistoryEntry CurrentEntry { get; }</c> for a TS
/// <c>readonly currentEntry: NavigationHistoryEntry | null</c>
/// declaration -- losing the nullability and producing a CS8618
/// warning in the consumer.
/// </summary>
public class GetPropertyTypesNullableTests
{
    private static readonly GeneratorOptions WasmOptions = new(
        SupportsGenerics: false,
        TypeName: "Permissions",
        Implementation: "window.navigator.permissions",
        IsWebAssembly: true);

    private static readonly GeneratorOptions ServerOptions = new(
        SupportsGenerics: false,
        TypeName: "Permissions",
        Implementation: "window.navigator.permissions",
        IsWebAssembly: false);

    [Fact]
    public void Primitive_NonNullable_Wasm_EmitsBareType()
    {
        var property = new CSharpProperty("accuracy", "number");
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("double", returnType);
        Assert.Equal("double", bareType);
    }

    [Fact]
    public void Primitive_Nullable_Wasm_EmitsNullableSuffix()
    {
        var property = new CSharpProperty("altitude", "number | null", IsNullable: true);
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("double?", returnType);
        Assert.Equal("double?", bareType);
    }

    [Fact]
    public void NonPrimitive_NonNullable_Wasm_EmitsBareType()
    {
        var property = new CSharpProperty("entry", "NavigationHistoryEntry");
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("NavigationHistoryEntry", returnType);
        Assert.Equal("NavigationHistoryEntry", bareType);
    }

    [Fact]
    public void NonPrimitive_Nullable_Wasm_EmitsNullableSuffix()
    {
        var property = new CSharpProperty(
            "currentEntry",
            "NavigationHistoryEntry | null",
            IsNullable: true);
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("NavigationHistoryEntry?", returnType);
        Assert.Equal("NavigationHistoryEntry?", bareType);
    }

    [Fact]
    public void NonPrimitive_Nullable_Server_WrapsValueTaskAroundNullable()
    {
        var property = new CSharpProperty(
            "currentEntry",
            "NavigationHistoryEntry | null",
            IsNullable: true);
        var (returnType, bareType) = property.GetPropertyTypes(ServerOptions);

        Assert.Equal("ValueTask<NavigationHistoryEntry?>", returnType);
        Assert.Equal("NavigationHistoryEntry?", bareType);
    }

    [Fact]
    public void ArrayOfPrimitive_NonNullable_Wasm_EmitsArrayBareType()
    {
        var property = new CSharpProperty("segments", "number[]");
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("double[]", returnType);
        Assert.Equal("double[]", bareType);
    }

    [Fact]
    public void ArrayOfPrimitive_Nullable_Wasm_EmitsNullableArraySuffix()
    {
        var property = new CSharpProperty("segments", "number[] | null", IsNullable: true);
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("double[]?", returnType);
        Assert.Equal("double[]?", bareType);
    }

    [Fact]
    public void ArrayOfCustom_Nullable_Wasm_EmitsNullableArraySuffix()
    {
        var property = new CSharpProperty(
            "entries",
            "NavigationHistoryEntry[] | null",
            IsNullable: true);
        var (returnType, bareType) = property.GetPropertyTypes(WasmOptions);

        Assert.Equal("NavigationHistoryEntry[]?", returnType);
        Assert.Equal("NavigationHistoryEntry[]?", bareType);
    }
}
