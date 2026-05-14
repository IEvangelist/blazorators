// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class EndToEndGenerationTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    private static SyntaxTree? GetGeneratedFile(GeneratorDriverRunResult result, string fileName) =>
        result.GeneratedTrees.FirstOrDefault(t =>
            System.IO.Path.GetFileName(t.FilePath) == fileName);

    [Fact]
    public void Geolocation_InterfaceFile_DeclaresExpectedMembers()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var iface = GetGeneratedFile(result, "IGeolocationService.g.cs");

        Assert.NotNull(iface);
        var text = iface!.ToString();

        Assert.Contains("public partial interface IGeolocationService", text);
        Assert.Contains("void ClearWatch(double watchId)", text);
        Assert.Contains("void GetCurrentPosition(", text);
        Assert.Contains("double WatchPosition(", text);
        Assert.Contains("namespace Microsoft.JSInterop;", text);
    }

    [Fact]
    public void Geolocation_ImplementationFile_BindsAgainstIJSInProcessRuntime()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var impl = GetGeneratedFile(result, "GeolocationService.g.cs");

        Assert.NotNull(impl);
        var text = impl!.ToString();

        Assert.Contains("internal sealed class GeolocationService : IGeolocationService", text);
        Assert.Contains("IJSInProcessRuntime", text);
        Assert.Contains("ClearWatch(double watchId)", text);
        Assert.Contains("_javaScript.InvokeVoid(", text);
    }

    [Fact]
    public void Geolocation_DiExtensionsFile_RegistersServiceAndRuntime()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var di = GetGeneratedFile(result, "GeolocationServiceCollectionExtensions.g.cs");

        Assert.NotNull(di);
        var text = di!.ToString();

        Assert.Contains("public static class GeolocationServiceCollectionExtensions", text);
        Assert.Contains("AddSingleton<IJSInProcessRuntime>", text);
        Assert.Contains("IGeolocationService, GeolocationService", text);
    }

    [Fact]
    public void Geolocation_EmitsDependentDtoTypes()
    {
        // Geolocation's TypeScript declaration references several other
        // DOM types (GeolocationPosition, GeolocationCoordinates,
        // GeolocationPositionError, PositionOptions). The generator must
        // emit a DTO source file for each of them.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var emittedFileNames = result.GeneratedTrees
            .Select(t => System.IO.Path.GetFileName(t.FilePath))
            .ToArray();

        Assert.Contains("GeolocationPosition.g.cs", emittedFileNames);
        Assert.Contains("GeolocationCoordinates.g.cs", emittedFileNames);
        Assert.Contains("GeolocationPositionError.g.cs", emittedFileNames);
        Assert.Contains("PositionOptions.g.cs", emittedFileNames);
    }

    [Fact]
    public void LocalStorage_Generic_EmitsJsonTypeInfoOverloadsForGenericDescriptors()
    {
        // Regression for the generic descriptor pipeline: the
        // `JSAutoGenericInterop` attribute combined with descriptors
        // like `"setItem:value"` must produce overloads that take a
        // `JsonTypeInfo<T>` so consumers can supply AOT-friendly
        // serialization metadata.
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
        var iface = GetGeneratedFile(result, "ILocalStorageService.g.cs");

        Assert.NotNull(iface);
        var ifaceText = iface!.ToString();

        Assert.Contains("JsonTypeInfo", ifaceText);
        Assert.Contains("GetItem<TValue>", ifaceText);
        Assert.Contains("SetItem<TValue>", ifaceText);
    }

    [Fact]
    public void EmitsBuiltInAttributeAndHostingModelSources()
    {
        // `RegisterPostInitializationOutput` must publish the
        // attribute and hosting-model source files regardless of
        // whether any attribute is actually present on a syntax node.
        const string source = "namespace Empty { }";

        var result = GetRunResult(source);
        var fileNames = result.GeneratedTrees
            .Select(t => System.IO.Path.GetFileName(t.FilePath))
            .ToArray();

        Assert.Contains("JSAutoInteropAttribute.g.cs", fileNames);
        Assert.Contains("JSAutoGenericInteropAttribute.g.cs", fileNames);
        Assert.Contains("BlazorHostingModel.g.cs", fileNames);
        Assert.Contains("RecordCompat.g.cs", fileNames);
    }
}
