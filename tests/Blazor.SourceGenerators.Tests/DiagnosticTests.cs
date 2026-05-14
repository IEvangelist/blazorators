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
        const string source = @"
namespace Sample
{
    [JSAutoInterop(Implementation = ""window.foo"")]
    public partial interface IFooService { }
}";
        var result = GetRunResult(source);
        Assert.Contains(result.Diagnostics, d => d.Id == "BR0001");
    }

    [Fact]
    public void BR0002_ReportsWhenImplementationMissing()
    {
        const string source = @"
namespace Sample
{
    [JSAutoInterop(TypeName = ""Geolocation"")]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);
        Assert.Contains(result.Diagnostics, d => d.Id == "BR0002");
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
