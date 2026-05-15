// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Defensive parse-validity sweep: every file the generator emits must
/// be syntactically valid C# (regardless of whether the consumer's
/// reference assemblies resolve the symbols). Adds a safety net so
/// that T2.6's removal of `NormalizeWhitespace()` and subsequent emit
/// refactors (T3.8 / T3.9) can't silently emit malformed text.
/// </summary>
public class GeneratorOutputValidityTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    [Theory]
    [InlineData(GeolocationWasmSource)]
    [InlineData(GeolocationServerSource)]
    [InlineData(LocalStorageWasmGenericSource)]
    public void EveryGeneratedTree_ParsesWithoutSyntaxErrors(string source)
    {
        var result = GetRunResult(source);

        Assert.NotEmpty(result.GeneratedTrees);

        foreach (var tree in result.GeneratedTrees)
        {
            var parsed = CSharpSyntaxTree.ParseText(tree.ToString());
            var errors = parsed
                .GetDiagnostics()
                .Where(d => d.Severity is DiagnosticSeverity.Error)
                .ToArray();

            Assert.Empty(errors);
        }
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
