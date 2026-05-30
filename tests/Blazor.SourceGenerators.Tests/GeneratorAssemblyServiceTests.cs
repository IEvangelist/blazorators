// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// End-to-end coverage for the G2 "write even less" attribute defaults.
/// <para>
/// A single <c>[assembly: JSAutoService("Geolocation")]</c> at file scope
/// should produce the same triplet of artifacts
/// (<c>IGeolocationService.g.cs</c>, <c>GeolocationService.g.cs</c>,
/// <c>GeolocationServiceCollectionExtensions.g.cs</c>) the interface-anchored
/// <c>[JSAutoInterop] partial interface IGeolocationService</c> form
/// produces today. The parity test guards against silent drift between the
/// two paths.
/// </para>
/// </summary>
public class GeneratorAssemblyServiceTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    // Anchor-free, namespace-free shape: nothing but the assembly attribute.
    // Mirrors the minimum a consumer would have to write to opt in.
    private const string SingleNameSource = @"
[assembly: JSAutoService(""Geolocation"")]
";

    // Multi-name in a single attribute application.
    private const string MultiNameSource = @"
[assembly: JSAutoService(""Geolocation"", ""Storage"")]
";

    // Two separate attribute applications - allowed because the attribute
    // is marked AllowMultiple = true.
    private const string MultipleAttributesSource = @"
[assembly: JSAutoService(""Geolocation"")]
[assembly: JSAutoService(""Storage"")]
";

    [Fact]
    public void AssemblyAttribute_SingleName_GeneratesInterface()
    {
        var result = GetRunResult(SingleNameSource);

        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "IGeolocationService.g.cs");

        Assert.NotNull(generated);
    }

    [Fact]
    public void AssemblyAttribute_SingleName_GeneratesImplementation()
    {
        var result = GetRunResult(SingleNameSource);

        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");

        Assert.NotNull(generated);
    }

    [Fact]
    public void AssemblyAttribute_SingleName_GeneratesDependencyInjectionExtensions()
    {
        var result = GetRunResult(SingleNameSource);

        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "GeolocationServiceCollectionExtensions.g.cs");

        Assert.NotNull(generated);
        Assert.Contains(
            "AddSingleton<IGeolocationService, GeolocationService>",
            generated!.ToString());
    }

    [Fact]
    public void AssemblyAttribute_SingleName_MatchesInterfaceForm_ByteForByte()
    {
        // The explicit interface-anchored form that the assembly attribute
        // should match. Mirrors what G1 inference produces.
        const string interfaceSource = @"
[JSAutoInterop(
    TypeName = ""Geolocation"",
    Implementation = ""window.geolocation"")]
public partial interface IGeolocationService { }
";

        var assemblyResult = GetRunResult(SingleNameSource);
        var interfaceResult = GetRunResult(interfaceSource);

        var assemblyFiles = assemblyResult.GeneratedTrees
            .ToDictionary(t => Path.GetFileName(t.FilePath), t => t.ToString());
        var interfaceFiles = interfaceResult.GeneratedTrees
            .ToDictionary(t => Path.GetFileName(t.FilePath), t => t.ToString());

        // Both forms should produce the same set of files. If a new file
        // type is added on one path, this assertion surfaces the drift.
        Assert.Equal(
            interfaceFiles.Keys.OrderBy(k => k),
            assemblyFiles.Keys.OrderBy(k => k));

        foreach (var fileName in assemblyFiles.Keys)
        {
            // Byte-for-byte equality. Any difference here means the
            // assembly-attribute path produced subtly different output and
            // consumers would see a drift in their generated code by
            // switching forms.
            Assert.Equal(interfaceFiles[fileName], assemblyFiles[fileName]);
        }
    }

    [Fact]
    public void AssemblyAttribute_MultipleNames_GeneratesAllTriplets()
    {
        var result = GetRunResult(MultiNameSource);
        var fileNames = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToHashSet();

        Assert.Contains("IGeolocationService.g.cs", fileNames);
        Assert.Contains("GeolocationService.g.cs", fileNames);
        Assert.Contains("GeolocationServiceCollectionExtensions.g.cs", fileNames);

        Assert.Contains("IStorageService.g.cs", fileNames);
        Assert.Contains("StorageService.g.cs", fileNames);
        Assert.Contains("StorageServiceCollectionExtensions.g.cs", fileNames);
    }

    [Fact]
    public void AssemblyAttribute_RepeatedAttributes_GeneratesAllServices()
    {
        var result = GetRunResult(MultipleAttributesSource);
        var fileNames = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToHashSet();

        // Both services produce their triplet even though they came from
        // two separate [assembly: JSAutoService(...)] applications.
        Assert.Contains("IGeolocationService.g.cs", fileNames);
        Assert.Contains("IStorageService.g.cs", fileNames);
    }

    [Fact]
    public void AssemblyAttribute_ExplicitImplementation_WinsOverInferred()
    {
        // navigator.geolocation, not window.geolocation. Single-name form
        // honors the Implementation override; multi-name form ignores it.
        const string source = @"
[assembly: JSAutoService(""Geolocation"", Implementation = ""window.navigator.geolocation"")]
";

        var result = GetRunResult(source);
        var impl = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs")
            .ToString();

        Assert.Contains("window.navigator.geolocation", impl);
        Assert.DoesNotContain("\"window.geolocation\"", impl);
    }

    [Fact]
    public void AssemblyAttribute_UnknownTypeName_FiresBR0006()
    {
        // The DOM does not define a "BogusType". The generator should
        // surface BR0006 (TargetTypeNotFound) rather than silently emitting
        // an empty or partial file.
        const string source = @"
[assembly: JSAutoService(""BogusType"")]
";

        var result = GetRunResult(source);
        var diagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Contains(
            diagnostics,
            d => d.Id == "BR0006" && d.GetMessage().Contains("BogusType"));
    }

    [Fact]
    public void AssemblyAttribute_CollidingWithInterfaceForm_PrefersInterface()
    {
        // Critical "don't break anything" guarantee: if a consumer has BOTH
        // [assembly: JSAutoService("Geolocation")] AND
        // [JSAutoInterop] partial interface IGeolocationService, the
        // interface-anchored form is the authoritative source (it carries
        // the consumer's namespace, partial body, and any extra members).
        // Without dedup, AddSource would throw on duplicate hint names.
        const string source = @"
[assembly: JSAutoService(""Geolocation"")]

namespace MyApp.Interop
{
    [JSAutoInterop]
    public partial interface IGeolocationService { }
}
";

        var result = GetRunResult(source);

        // No exceptions from AddSource means we deduped correctly.
        // The single emitted IGeolocationService should live in the
        // consumer's namespace (MyApp.Interop), not the assembly attribute's
        // default (Microsoft.JSInterop).
        var iface = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == "IGeolocationService.g.cs")
            .ToString();

        Assert.Contains("namespace MyApp.Interop", iface);
    }
}
