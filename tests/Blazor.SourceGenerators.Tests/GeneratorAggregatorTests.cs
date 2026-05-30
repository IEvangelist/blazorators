// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// End-to-end coverage for the G4 "write less" aggregator extension:
/// <c>AddBlazoratorsAll(this IServiceCollection)</c>, a single per-
/// compilation DI helper that chains every per-service
/// <c>Add{ImplementationName}Services()</c> the generator produced.
/// </summary>
public class GeneratorAggregatorTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    private const string AggregatorFileName =
        "BlazoratorsServiceCollectionExtensions.g.cs";

    [Fact]
    public void Aggregator_ZeroServices_NotEmitted()
    {
        // Consumer compilation with no JS-interop attributes at all. The
        // aggregator file must not appear in the output because there are
        // no per-service Add{X}Services() methods to chain.
        var result = GetRunResult("// no attributes anywhere");

        var aggregator = result.GeneratedTrees
            .FirstOrDefault(t => Path.GetFileName(t.FilePath) == AggregatorFileName);

        Assert.Null(aggregator);
    }

    [Fact]
    public void Aggregator_SingleService_EmitsFileWithOneCall()
    {
        const string source = @"
[assembly: JSAutoService(""Geolocation"")]
";
        var result = GetRunResult(source);
        var aggregator = result.GeneratedTrees
            .FirstOrDefault(t => Path.GetFileName(t.FilePath) == AggregatorFileName);

        Assert.NotNull(aggregator);

        var text = aggregator!.ToString();

        Assert.Contains("public static class BlazoratorsServiceCollectionExtensions", text);
        Assert.Contains("AddBlazoratorsAll", text);
        Assert.Contains("services.AddGeolocationServices()", text);
        Assert.Contains("return services", text);
    }

    [Fact]
    public void Aggregator_MultipleServices_EmitsCallsInDeterministicOrder()
    {
        // Three services requested in non-alphabetical order. The
        // aggregator should emit them sorted by extension method name
        // (StringComparer.Ordinal), so the output is stable across
        // compilations regardless of consumer source ordering.
        const string source = @"
[assembly: JSAutoService(""Storage"")]
[assembly: JSAutoService(""Geolocation"")]
[assembly: JSAutoService(""Clipboard"")]
";
        var result = GetRunResult(source);
        var text = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == AggregatorFileName)
            .ToString();

        var clipboard = text.IndexOf("AddClipboardServices", StringComparison.Ordinal);
        var geolocation = text.IndexOf("AddGeolocationServices", StringComparison.Ordinal);
        var storage = text.IndexOf("AddStorageServices", StringComparison.Ordinal);

        Assert.True(clipboard > 0);
        Assert.True(geolocation > clipboard);
        Assert.True(storage > geolocation);
    }

    [Fact]
    public void Aggregator_CollidingFormDeduplicates_OneCallPerService()
    {
        // Interface-anchored + assembly-attribute form requesting the same
        // service should collapse to a single AddGeolocationServices() call
        // in the aggregator. Without dedup the consumer would see the same
        // service registered twice.
        const string source = @"
[assembly: JSAutoService(""Geolocation"")]

namespace MyApp.Interop
{
    [JSAutoInterop]
    public partial interface IGeolocationService { }
}
";
        var result = GetRunResult(source);
        var text = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == AggregatorFileName)
            .ToString();

        // Count occurrences of the method call (excluding the method
        // definition match, which doesn't include the parenthesized form).
        var firstHit = text.IndexOf("AddGeolocationServices()", StringComparison.Ordinal);
        Assert.True(firstHit > 0);

        var secondHit = text.IndexOf("AddGeolocationServices()", firstHit + 1, StringComparison.Ordinal);
        Assert.True(secondHit < 0, "Aggregator emitted the same service twice");
    }

    [Fact]
    public void Aggregator_InvalidService_NotIncluded()
    {
        // BogusType produces BR0006 and never emits a per-service DI
        // extension. The aggregator should consequently skip it; otherwise
        // the generated code would reference an undefined
        // AddBogusTypeServices() method and fail to compile.
        const string source = @"
[assembly: JSAutoService(""Geolocation"")]
[assembly: JSAutoService(""BogusType"")]
";
        var result = GetRunResult(source);
        var aggregator = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == AggregatorFileName)
            .ToString();

        Assert.Contains("AddGeolocationServices", aggregator);
        Assert.DoesNotContain("AddBogusTypeServices", aggregator);
    }

    [Fact]
    public void Aggregator_GeneratedCode_Compiles()
    {
        // Parse the emitted aggregator and confirm it produces a syntax
        // tree with no errors. Catches missing usings, wrong namespace,
        // malformed method body.
        const string source = @"
[assembly: JSAutoService(""Geolocation"")]
[assembly: JSAutoService(""Storage"")]
";
        var result = GetRunResult(source);
        var tree = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == AggregatorFileName);

        var diagnostics = tree.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var root = (CompilationUnitSyntax)tree.GetRoot();

        // Find the AddBlazoratorsAll method and confirm it has at least
        // two AddXServices() invocations chained off `services`.
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "AddBlazoratorsAll");

        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString().StartsWith(
                "services.Add", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, invocations.Length);
    }
}
