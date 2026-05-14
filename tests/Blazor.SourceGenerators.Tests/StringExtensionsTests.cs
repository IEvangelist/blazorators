// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("a", "A")]
    [InlineData("hello", "Hello")]
    [InlineData("Hello", "Hello")]
    public void CapitalizeFirstLetter(string input, string expected)
    {
        Assert.Equal(expected, input.CapitalizeFirstLetter());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("A", "a")]
    [InlineData("Hello", "hello")]
    [InlineData("hello", "hello")]
    public void LowerCaseFirstLetter(string input, string expected)
    {
        Assert.Equal(expected, input.LowerCaseFirstLetter());
    }

    [Theory]
    [InlineData("window.localStorage", "LocalStorageService")]
    [InlineData("localStorage", "LocalStorageService")]
    [InlineData("window.navigator.geolocation", "GeolocationService")]
    [InlineData("", "Service")]
    public void ToImplementationName(string input, string expected)
    {
        Assert.Equal(expected, input.ToImplementationName());
    }

    [Theory]
    [InlineData("window.localStorage", "ILocalStorageService")]
    [InlineData("localStorage", "ILocalStorageService")]
    [InlineData("", "IService")]
    public void ToInterfaceName(string input, string expected)
    {
        Assert.Equal(expected, input.ToInterfaceName());
    }

    [Fact]
    public void ToImplementationName_TrailingDotDoesNotCrash()
    {
        // Regression: a path ending in '.' used to index past the end via
        // 'LastIndexOf(".") + 1', returning an empty string and then attempting
        // to capitalize via 'name[0]'. The hardened version falls back to the
        // original input instead.
        Assert.Equal("Window.Service", "window.".ToImplementationName());
    }
}
