// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class HostingModelTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    [Fact]
    public void Default_GeneratesWebAssemblyExtensions()
    {
        // The attribute default is WebAssembly: lifetime is Singleton and
        // the service is resolved as `IJSInProcessRuntime` (synchronous).
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var diExt = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("GeolocationServiceCollectionExtensions"));

        Assert.NotNull(diExt);
        var diText = diExt!.ToString();

        Assert.Contains("AddSingleton<IJSInProcessRuntime>", diText);
        Assert.Contains("IJSInProcessRuntime", diText);
        Assert.DoesNotContain("AddScoped", diText);
    }

    [Fact]
    public void HostingModelServer_GeneratesScopedAsyncExtensions()
    {
        // `HostingModel = BlazorHostingModel.Server` flips `IsWebAssembly`
        // to false: lifetime becomes Scoped and the DI extension binds
        // directly against the (async) `IJSRuntime`.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"",
        HostingModel = BlazorHostingModel.Server)]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var diExt = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("GeolocationServiceCollectionExtensions"));

        Assert.NotNull(diExt);
        var diText = diExt!.ToString();

        Assert.Contains("AddScoped", diText);
        Assert.DoesNotContain("AddSingleton<IJSInProcessRuntime>", diText);
    }

    [Fact]
    public void Default_GeneratesSynchronousVoidInvocations()
    {
        // WebAssembly mode uses synchronous `InvokeVoid` calls (no Async
        // suffix) in the implementation file.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var impl = result.GeneratedTrees
            .FirstOrDefault(t => System.IO.Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");

        Assert.NotNull(impl);
        var implText = impl!.ToString();

        Assert.Contains("_javaScript.InvokeVoid(", implText);
    }

    [Fact]
    public void HostingModelServer_GeneratesAsynchronousInvocations()
    {
        // Server mode uses the async invocation variants
        // (`InvokeVoidAsync` / `InvokeAsync`) on `IJSRuntime`.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"",
        HostingModel = BlazorHostingModel.Server)]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var impl = result.GeneratedTrees
            .FirstOrDefault(t => System.IO.Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");

        Assert.NotNull(impl);
        var implText = impl!.ToString();

        Assert.Contains("InvokeVoidAsync(", implText);
    }
}
