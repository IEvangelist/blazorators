// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class JavaScriptInteropTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    const string GeolocationAutoInterop = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

    static bool ContainsFile(GeneratorDriverRunResult result, string fileNameFragment) =>
        result.GeneratedTrees.Any(t => t.FilePath.Contains(fileNameFragment));

    [Fact]
    public void GeneratesOutput_ForJSAutoInterop()
    {
        var result = GetRunResult(GeolocationAutoInterop);

        Assert.True(ContainsFile(result, "IGeolocationService"));
        Assert.True(ContainsFile(result, "GeolocationServiceCollectionExtensions"));
    }

    [Fact]
    public void GeneratesOutput_WhenJSAutoInteropIsNotFirstAttribute()
    {
        const string source = @"
using System;
namespace Microsoft.JSInterop
{
    [Obsolete(""legacy""), JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);

        Assert.True(
            ContainsFile(result, "GeolocationServiceCollectionExtensions"),
            "Generator must scan every attribute on the interface, not just the first.");
    }

    [Fact]
    public void GeneratesOutput_WhenAttributeUsesFullyQualifiedName()
    {
        const string source = @"
namespace Consumer
{
    [Microsoft.JSInterop.JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);

        Assert.True(
            ContainsFile(result, "GeolocationServiceCollectionExtensions"),
            "Generator must recognize the attribute when used with a fully-qualified name.");
    }

    [Fact]
    public void GeneratesOutput_WhenAttributeUsesExplicitAttributeSuffix()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInteropAttribute(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);

        Assert.True(ContainsFile(result, "GeolocationServiceCollectionExtensions"));
    }

    [Fact]
    public void JSAutoInterop_DoesNotEnableGenerics()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"")]
    public partial interface ILocalStorageService { }
}";

        var result = GetRunResult(source);
        var iface = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("ILocalStorageService"));

        Assert.NotNull(iface);
        Assert.DoesNotContain("JsonTypeInfo", iface!.ToString());
    }

    [Fact]
    public void JSAutoGenericInterop_EnablesGenerics()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = new[] { ""getItem"", ""setItem:value"" })]
    public partial interface ILocalStorageService { }
}";

        var result = GetRunResult(source);
        var iface = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("ILocalStorageService"));

        Assert.NotNull(iface);
        Assert.Contains("JsonTypeInfo", iface!.ToString());
    }

    [Fact]
    public void EmitsDiagnostic_WhenInterfaceIsNotPartial()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public interface IGeolocationService { }
}";

        var result = GetRunResult(source);

        Assert.False(
            ContainsFile(result, "GeolocationServiceCollectionExtensions"),
            "Non-partial interfaces must not produce generated output.");

        Assert.Contains(result.Diagnostics, d => d.Id == "BR0005");
    }

    [Fact]
    public void EmitsDiagnostic_WhenTypeNameNotFoundInLibDom()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""ThisTypeDoesNotExistAnywhere"",
        Implementation = ""window.bogus"")]
    public partial interface IBogusService { }
}";

        var result = GetRunResult(source);

        Assert.Contains(result.Diagnostics, d => d.Id == "BR0006");
    }
}
