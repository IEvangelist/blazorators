// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// End-to-end coverage for the G1 "write less" attribute defaults. A bare
/// <c>[JSAutoInterop] partial interface IGeolocationService</c> should
/// produce the same generated artifacts as the verbose form that supplies
/// <c>TypeName</c> and <c>Implementation</c> explicitly. The parity test
/// guards against silent drift between the two paths.
/// </summary>
public class GeneratorInferenceTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    private const string BareSource = @"
namespace MyApp.Interop
{
    [JSAutoInterop]
    public partial interface IGeolocationService { }
}";

    // Matches what G1 inference produces for IGeolocationService:
    //  - TypeName        => "Geolocation"
    //  - Implementation  => "window.geolocation"
    private const string ExplicitSource = @"
namespace MyApp.Interop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.geolocation"")]
    public partial interface IGeolocationService { }
}";

    [Fact]
    public void BareAttribute_GeneratesInterface()
    {
        var result = GetRunResult(BareSource);

        // The interface generator names the file after the host interface
        // (IGeolocationService) - both the bare and explicit forms produce
        // a file with the same name.
        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "IGeolocationService.g.cs");

        Assert.NotNull(generated);
    }

    [Fact]
    public void BareAttribute_GeneratesImplementation()
    {
        var result = GetRunResult(BareSource);

        // ToImplementationName("window.geolocation") => "Geolocation" + "Service".
        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");

        Assert.NotNull(generated);
    }

    [Fact]
    public void BareAttribute_GeneratesDependencyInjectionExtensions()
    {
        var result = GetRunResult(BareSource);

        var generated = result.GeneratedTrees.FirstOrDefault(
            t => Path.GetFileName(t.FilePath) == "GeolocationServiceCollectionExtensions.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("AddSingleton<IGeolocationService, GeolocationService>", generated!.ToString());
    }

    [Fact]
    public void BareAttribute_MatchesExplicitForm_ByteForByte()
    {
        var bareResult = GetRunResult(BareSource);
        var explicitResult = GetRunResult(ExplicitSource);

        var bareFiles = bareResult.GeneratedTrees
            .ToDictionary(t => Path.GetFileName(t.FilePath), t => t.ToString());
        var explicitFiles = explicitResult.GeneratedTrees
            .ToDictionary(t => Path.GetFileName(t.FilePath), t => t.ToString());

        Assert.Equal(explicitFiles.Keys.OrderBy(k => k), bareFiles.Keys.OrderBy(k => k));

        foreach (var fileName in bareFiles.Keys)
        {
            // Inferred form must produce byte-identical output. If this
            // assert fires, either inference picked the wrong default or
            // a downstream emit step depends on something other than the
            // (TypeName, Implementation) pair.
            Assert.Equal(explicitFiles[fileName], bareFiles[fileName]);
        }
    }

    [Fact]
    public void BareAttribute_Without_Service_Suffix_Infers_TypeName()
    {
        // The "Service" suffix is optional in the convention; an interface
        // named ILocalStorage should still infer TypeName = "LocalStorage".
        // The DOM doesn't have an interface called "LocalStorage" though,
        // so this would normally fall through to BR0006. We assert on the
        // attribute parsing path indirectly by checking the diagnostic.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoInterop]
    public partial interface ILocalStorage { }
}";

        var result = GetRunResult(source);
        var diagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // If inference works, we expect BR0006 with TypeName="LocalStorage"
        // in the message (DOM has "Storage", not "LocalStorage").
        Assert.Contains(diagnostics, d => d.Id == "BR0006" && d.GetMessage().Contains("LocalStorage"));
    }

    [Fact]
    public void Explicit_Implementation_Wins_Over_Inferred()
    {
        // Consumer wants IGeolocationService but the real path is
        // `navigator.geolocation`, not `window.geolocation`. They opt out
        // of the implementation inference by supplying Implementation
        // explicitly. The generated DI extension must reflect that.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoInterop(Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var result = GetRunResult(source);
        var impl = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs")
            .ToString();

        // The implementation body invokes a JS path derived from
        // Implementation - the explicit value should win, not the inferred
        // "window.geolocation".
        Assert.Contains("window.navigator.geolocation", impl);
        Assert.DoesNotContain("\"window.geolocation\"", impl);
    }
}
