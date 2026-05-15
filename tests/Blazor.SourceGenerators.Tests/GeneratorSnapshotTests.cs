// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// End-to-end snapshot tests pinning the exact text produced by the
/// generator for representative interop scenarios. These exist so
/// downstream refactors (T2.6 skip-NormalizeWhitespace, T3.8/T3.9
/// implementation/DTO emit refactors) can be landed safely - if the
/// output changes byte-for-byte, the snapshot assertion catches it.
/// </summary>
public class GeneratorSnapshotTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    private static string ReadFile(GeneratorDriverRunResult result, string fileName) =>
        result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == fileName)
            .ToString();

    [Fact]
    public void Geolocation_WebAssembly_Snapshot_Interface()
    {
        var result = GetRunResult(GeolocationWasmSource);
        var actual = ReadFile(result, "IGeolocationService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Wasm",
            fileName: "IGeolocationService.g.cs",
            actual: actual);
    }

    [Fact]
    public void Geolocation_WebAssembly_Snapshot_Implementation()
    {
        var result = GetRunResult(GeolocationWasmSource);
        var actual = ReadFile(result, "GeolocationService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Wasm",
            fileName: "GeolocationService.g.cs",
            actual: actual);
    }

    [Fact]
    public void Geolocation_WebAssembly_Snapshot_ServiceCollectionExtensions()
    {
        var result = GetRunResult(GeolocationWasmSource);
        var actual = ReadFile(result, "GeolocationServiceCollectionExtensions.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Wasm",
            fileName: "GeolocationServiceCollectionExtensions.g.cs",
            actual: actual);
    }

    [Fact]
    public void Geolocation_Server_Snapshot_Implementation()
    {
        var result = GetRunResult(GeolocationServerSource);
        var actual = ReadFile(result, "GeolocationService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Server",
            fileName: "GeolocationService.g.cs",
            actual: actual);
    }

    [Fact]
    public void LocalStorage_WebAssembly_Generic_Snapshot_Interface()
    {
        var result = GetRunResult(LocalStorageWasmGenericSource);
        var actual = ReadFile(result, "ILocalStorageService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "LocalStorage_Wasm_Generic",
            fileName: "ILocalStorageService.g.cs",
            actual: actual);
    }

    [Fact]
    public void Geolocation_WebAssembly_Snapshot_DependentDto_GeolocationPosition()
    {
        var result = GetRunResult(GeolocationWasmSource);
        var actual = ReadFile(result, "GeolocationPosition.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Wasm",
            fileName: "GeolocationPosition.g.cs",
            actual: actual);
    }

    [Fact]
    public void Geolocation_WebAssembly_Snapshot_DependentDto_GeolocationCoordinates()
    {
        var result = GetRunResult(GeolocationWasmSource);
        var actual = ReadFile(result, "GeolocationCoordinates.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Geolocation_Wasm",
            fileName: "GeolocationCoordinates.g.cs",
            actual: actual);
    }

    private const string GeolocationWasmSource = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

    private const string GeolocationServerSource = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"",
        HostingModel = BlazorHostingModel.Server)]
    public partial interface IGeolocationService { }
}";

    private const string LocalStorageWasmGenericSource = @"
namespace Microsoft.JSInterop
{
    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = new[] { ""getItem"", ""setItem:value"" })]
    public partial interface ILocalStorageService { }
}";
}
