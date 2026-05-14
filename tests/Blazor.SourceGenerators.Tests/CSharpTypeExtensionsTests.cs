// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class CSharpTypeExtensionsTests
{
    [Fact]
    public void IsGenericParameter_DoesNotMatch_WhenDescriptorMethodNameIsSubstring()
    {
        // Regression: descriptor "setItem:value" must not match method "set"
        // (the original implementation used StartsWith which produced a false positive).
        var options = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["setItem:value"]);

        var parameter = new CSharpType("value", "any");

        Assert.False(parameter.IsGenericParameter(methodName: "set", options));
    }

    [Fact]
    public void IsGenericParameter_Matches_WhenDescriptorMethodNameMatchesExactly()
    {
        var options = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["setItem:value"]);

        var parameter = new CSharpType("value", "any");

        Assert.True(parameter.IsGenericParameter(methodName: "setItem", options));
    }

    [Fact]
    public void IsGenericParameter_DoesNotMatch_WhenDescriptorParameterNameIsSubstring()
    {
        // Regression: descriptor "setItem:valueExtra" must not match parameter "value".
        var options = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["setItem:valueExtra"]);

        var parameter = new CSharpType("value", "any");

        Assert.False(parameter.IsGenericParameter(methodName: "setItem", options));
    }

    [Fact]
    public void IsGenericParameter_ReturnsFalse_WhenDescriptorHasNoColon()
    {
        // Descriptors without ':' describe a generic *return type*, not a generic parameter.
        var options = new GeneratorOptions(
            SupportsGenerics: true,
            GenericMethodDescriptors: ["getItem"]);

        var parameter = new CSharpType("key", "string");

        Assert.False(parameter.IsGenericParameter(methodName: "getItem", options));
    }

    [Fact]
    public void IsGenericParameter_ReturnsFalse_WhenDescriptorsNull()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);
        var parameter = new CSharpType("value", "any");

        Assert.False(parameter.IsGenericParameter(methodName: "setItem", options));
    }
}
