// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Unit tests for <c>OptionsInference</c> - the helper that gives consumers
/// a "write less" path on <c>[JSAutoInterop]</c> by inferring
/// <c>TypeName</c> / <c>Implementation</c> from the host interface name.
/// </summary>
public class OptionsInferenceTests
{
    [Theory]
    [InlineData("IGeolocationService", "Geolocation")]
    [InlineData("ILocalStorageService", "LocalStorage")]
    [InlineData("ILocalStorage", "LocalStorage")]
    [InlineData("IWidget", "Widget")]
    [InlineData("WidgetService", "Widget")]
    [InlineData("Widget", "Widget")]
    [InlineData("Inline", "Inline")]               // Leading I + lowercase second char => no strip.
    [InlineData("IGeolocation", "Geolocation")]     // Already missing Service => only I stripped.
    [InlineData("IConsoleService", "Console")]
    public void InferTypeName_ReturnsExpected(string interfaceName, string expected)
    {
        var actual = OptionsInference.InferTypeName(interfaceName);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("IService")]      // Strip I => "Service"; strip Service => empty => null.
    [InlineData("Service")]       // Strip Service => empty => null.
    public void InferTypeName_ReturnsNullForDegenerate(string? interfaceName)
    {
        var actual = OptionsInference.InferTypeName(interfaceName);
        Assert.Null(actual);
    }

    [Fact]
    public void InferTypeName_SingleChar_I_StaysAsI()
    {
        // "I" alone fails the (length >= 2) leading-I guard so the strip is
        // skipped; "I" does not end with "Service" so that strip is also
        // skipped. The result is a valid (if useless) identifier - downstream
        // BR0006 handles the eventual TS lookup failure.
        var actual = OptionsInference.InferTypeName("I");
        Assert.Equal("I", actual);
    }

    [Theory]
    [InlineData("Geolocation", "window.geolocation")]
    [InlineData("LocalStorage", "window.localStorage")]
    [InlineData("Console", "window.console")]
    [InlineData("A", "window.a")]
    [InlineData("AB", "window.aB")]
    [InlineData("", "window")]
    public void InferImplementation_CamelCasesUnderWindow(string typeName, string expected)
    {
        var actual = OptionsInference.InferImplementation(typeName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ApplyInferredDefaults_FillsBoth_WhenConsumerProvidesNeither()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);

        var actual = OptionsInference.ApplyInferredDefaults(options, "IGeolocationService");

        Assert.Equal("Geolocation", actual.TypeName);
        Assert.Equal("window.geolocation", actual.Implementation);
    }

    [Fact]
    public void ApplyInferredDefaults_PreservesExplicitTypeName_StillInfersImplementation()
    {
        var options = new GeneratorOptions(SupportsGenerics: false, TypeName: "Storage");

        var actual = OptionsInference.ApplyInferredDefaults(options, "ILocalStorageService");

        Assert.Equal("Storage", actual.TypeName);                       // Consumer wins.
        Assert.Equal("window.storage", actual.Implementation);          // Inferred from explicit TypeName.
    }

    [Fact]
    public void ApplyInferredDefaults_PreservesExplicitImplementation()
    {
        var options = new GeneratorOptions(
            SupportsGenerics: false,
            Implementation: "window.navigator.geolocation");

        var actual = OptionsInference.ApplyInferredDefaults(options, "IGeolocationService");

        Assert.Equal("Geolocation", actual.TypeName);                              // Inferred from interface name.
        Assert.Equal("window.navigator.geolocation", actual.Implementation);       // Consumer wins.
    }

    [Fact]
    public void ApplyInferredDefaults_NoOp_WhenBothExplicit()
    {
        var options = new GeneratorOptions(
            SupportsGenerics: false,
            TypeName: "Geolocation",
            Implementation: "window.navigator.geolocation");

        var actual = OptionsInference.ApplyInferredDefaults(options, "IGeolocationService");

        Assert.Same(options, actual);
    }

    [Fact]
    public void ApplyInferredDefaults_NoOp_WhenInferenceFails_AndBothNull()
    {
        // "IService" => InferTypeName returns null (empty after both strips).
        // Inference is skipped so BR0001/BR0002 can fire downstream.
        var options = new GeneratorOptions(SupportsGenerics: false);

        var actual = OptionsInference.ApplyInferredDefaults(options, "IService");

        Assert.Same(options, actual);
        Assert.Null(actual.TypeName);
        Assert.Null(actual.Implementation);
    }
}
