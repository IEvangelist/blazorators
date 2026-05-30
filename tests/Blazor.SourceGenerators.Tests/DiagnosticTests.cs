// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class DiagnosticTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    [Fact]
    public void BR0001_ReportsWhenTypeNameMissing()
    {
        // Post-G1, BR0001 fires only when TypeName inference fails. The
        // interface name "IService" strips to empty after both the leading
        // "I" and trailing "Service" suffixes are removed, so inference
        // can't produce a sensible TypeName and BR0001 surfaces.
        const string source = @"
namespace Sample
{
    [JSAutoInterop(Implementation = ""window.foo"")]
    public partial interface IService { }
}";
        var result = GetRunResult(source);
        Assert.Contains(result.Diagnostics, d => d.Id == "BR0001");
    }

    [Fact]
    public void BR0002_DescriptorIsRegistered_ButNotTriggeredViaInferenceSurface()
    {
        // BR0002 ("Implementation required") was originally triggered by
        // an attribute that supplied TypeName but omitted Implementation.
        // Post-G1, that path now flows through OptionsInference, which
        // synthesises an Implementation from the resolved TypeName. So
        // BR0002 is no longer reachable via the consumer attribute surface
        // - it remains a declared diagnostic so future code paths (or a
        // direct GeneratorOptions construction) can still report it.
        const string source = @"
namespace Sample
{
    [JSAutoInterop(TypeName = ""Geolocation"")]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        // No error-level BR0002; the inferred Implementation "window.geolocation"
        // takes effect and generation proceeds normally.
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "BR0002");

        // The descriptor itself remains available for code paths that bypass
        // inference (sanity check on the public diagnostic surface).
        Assert.Equal(
            "BR0002",
            Blazor.SourceGenerators.Diagnostics.Descriptors.ImplementationRequiredDiagnostic.Id);
    }

    [Fact]
    public void BR0005_ReportsWhenInterfaceIsNotPartial()
    {
        const string source = @"
namespace Sample
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public interface IGeolocationService { }
}";
        var result = GetRunResult(source);
        var diag = Assert.Single(result.Diagnostics, d => d.Id == "BR0005");
        Assert.Contains("IGeolocationService", diag.GetMessage());
    }

    [Fact]
    public void BR0006_ReportsWhenTypeNameNotFoundInDomDeclarations()
    {
        const string source = @"
namespace Sample
{
    [JSAutoInterop(
        TypeName = ""ThisTypeDoesNotExistInLibDomDts"",
        Implementation = ""window.nope"")]
    public partial interface INopeService { }
}";
        var result = GetRunResult(source);
        var diag = Assert.Single(result.Diagnostics, d => d.Id == "BR0006");
        Assert.Contains("ThisTypeDoesNotExistInLibDomDts", diag.GetMessage());
    }

    [Fact]
    public void NoDiagnostics_OnValidGeolocationInteropDeclaration()
    {
        const string source = @"
namespace Sample
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
