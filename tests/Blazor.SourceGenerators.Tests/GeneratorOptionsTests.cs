// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class GeneratorOptionsTests
{
    [Fact]
    public void Parsers_ReturnsSameInstance_OnRepeatedAccess()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);

        var first = options.Parsers;
        var second = options.Parsers;

        Assert.Same(first, second);
    }

    [Fact]
    public void Parsers_DoesNotGrow_OnRepeatedAccess()
    {
        // Regression: previously the getter re-added a fresh 'TypeDeclarationParser'
        // (reference equality) every access, so the set grew unboundedly whenever
        // 'TypeDeclarationSources' was non-empty.
        var options = new GeneratorOptions(
            SupportsGenerics: false,
            TypeDeclarationSources: ["foo.d.ts", "bar.d.ts"]);

        var initialCount = options.Parsers.Count;

        for (var i = 0; i < 5; i++)
        {
            _ = options.Parsers;
        }

        Assert.Equal(initialCount, options.Parsers.Count);
    }

    [Fact]
    public void Parsers_ReturnsDefaultParser_WhenNoSourcesProvided()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);

        Assert.Contains(TypeDeclarationParser.Default, options.Parsers);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForIdenticalContentIncludingArrays()
    {
        // Regression: the synthesized record Equals does reference equality on
        // arrays, which defeats Roslyn incremental caching. GeneratorOptions
        // must compare arrays element-wise so identical inputs produce equal
        // pipeline outputs.
        var a = new GeneratorOptions(
            SupportsGenerics: true,
            TypeName: "Storage",
            Implementation: "window.localStorage",
            OnlyGeneratePureJS: false,
            Url: "https://example.org",
            GenericMethodDescriptors: ["getItem:value", "setItem:value"],
            PureJavaScriptOverrides: ["clear", "key:index"],
            TypeDeclarationSources: ["foo.d.ts", "bar.d.ts"],
            IsWebAssembly: true);

        var b = new GeneratorOptions(
            SupportsGenerics: true,
            TypeName: "Storage",
            Implementation: "window.localStorage",
            OnlyGeneratePureJS: false,
            Url: "https://example.org",
            GenericMethodDescriptors: ["getItem:value", "setItem:value"],
            PureJavaScriptOverrides: ["clear", "key:index"],
            TypeDeclarationSources: ["foo.d.ts", "bar.d.ts"],
            IsWebAssembly: true);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenArrayElementsDiffer()
    {
        var a = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["getItem:value"]);

        var b = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["setItem:value"]);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenArrayLengthsDiffer()
    {
        var a = new GeneratorOptions(
            SupportsGenerics: true,
            PureJavaScriptOverrides: ["clear"]);

        var b = new GeneratorOptions(
            SupportsGenerics: true,
            PureJavaScriptOverrides: ["clear", "key:index"]);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_TreatsNullAndEmptyArraysAsDistinct()
    {
        var withNull = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: null);

        var withEmpty = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: []);

        Assert.NotEqual(withNull, withEmpty);
    }

    [Fact]
    public void Equals_TreatsBothNullArraysAsEqual()
    {
        var a = new GeneratorOptions(SupportsGenerics: true, TypeName: "T");
        var b = new GeneratorOptions(SupportsGenerics: true, TypeName: "T");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
