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

    /// <summary>
    /// End-to-end coverage for <c>Promise&lt;T&gt;</c> return-type
    /// unwrapping. The <c>Permissions</c> DOM interface declares
    /// <c>query(permissionDesc: PermissionDescriptor): Promise&lt;PermissionStatus&gt;</c>
    /// -- before <c>Promise&lt;T&gt;</c> support landed, the generated
    /// method signature contained a verbatim <c>Promise&lt;PermissionStatus&gt;</c>
    /// token (not a CLR type) and the call site invoked the synchronous
    /// <c>Invoke&lt;T&gt;</c> overload on a value that cannot resolve
    /// synchronously. The snapshot now pins the corrected behaviour:
    /// the return type is <c>ValueTask&lt;PermissionStatus&gt;</c>, the
    /// call site uses <c>InvokeAsync</c>, and the method name carries
    /// the <c>Async</c> suffix even under the WebAssembly hosting
    /// model.
    /// </summary>
    [Fact]
    public void Permissions_WebAssembly_Snapshot_Implementation()
    {
        var result = GetRunResult(PermissionsWasmSource);
        var actual = ReadFile(result, "PermissionsService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Permissions_Wasm",
            fileName: "PermissionsService.g.cs",
            actual: actual);
    }

    [Fact]
    public void Permissions_WebAssembly_Snapshot_Interface()
    {
        var result = GetRunResult(PermissionsWasmSource);
        var actual = ReadFile(result, "IPermissionsService.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Permissions_Wasm",
            fileName: "IPermissionsService.g.cs",
            actual: actual);
    }

    /// <summary>
    /// Pins the generated <c>PermissionDescriptor</c> DTO emitted as a
    /// transitive dependency of <c>Permissions.query</c>. The DTO
    /// carries a single <c>name: PermissionName</c> property where
    /// <c>PermissionName</c> is a TS string-union type alias ("geolocation"
    /// | "notifications" | ...). The alias resolver collapses string
    /// unions to <c>string</c>, so the emitted property must be a
    /// plain C# <c>string</c> with the matching JSON property name.
    /// </summary>
    [Fact]
    public void Permissions_WebAssembly_Snapshot_DependentDto_PermissionDescriptor()
    {
        var result = GetRunResult(PermissionsWasmSource);
        var actual = ReadFile(result, "PermissionDescriptor.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Permissions_Wasm",
            fileName: "PermissionDescriptor.g.cs",
            actual: actual);
    }

    /// <summary>
    /// Pins the generated <c>PermissionStatus</c> DTO. The interface
    /// declares an <c>onchange</c> arrow-function property (filtered)
    /// and an <c>addEventListener</c> overload set (filtered), so only
    /// <c>name</c> and <c>state</c> survive into the emitted class.
    /// </summary>
    [Fact]
    public void Permissions_WebAssembly_Snapshot_DependentDto_PermissionStatus()
    {
        var result = GetRunResult(PermissionsWasmSource);
        var actual = ReadFile(result, "PermissionStatus.g.cs");

        SnapshotAsserter.AssertMatchesSnapshot(
            scenario: "Permissions_Wasm",
            fileName: "PermissionStatus.g.cs",
            actual: actual);
    }

    private const string PermissionsWasmSource = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Permissions"",
        Implementation = ""window.navigator.permissions"")]
    public partial interface IPermissionsService { }
}";

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
