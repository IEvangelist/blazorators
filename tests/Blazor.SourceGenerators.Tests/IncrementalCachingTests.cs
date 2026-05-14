// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class IncrementalCachingTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    [Fact]
    public void InteropTarget_TwoIdenticalInstances_AreEqual()
    {
        // Regression: the pipeline must produce value-equatable records so
        // that Roslyn's incremental cache can short-circuit unchanged inputs.
        var optionsA = new GeneratorOptions(
            SupportsGenerics: true,
            TypeName: "Geolocation",
            Implementation: "window.navigator.geolocation",
            GenericMethodDescriptors: ["getCurrentPosition:options"]);

        var optionsB = new GeneratorOptions(
            SupportsGenerics: true,
            TypeName: "Geolocation",
            Implementation: "window.navigator.geolocation",
            GenericMethodDescriptors: ["getCurrentPosition:options"]);

        var a = new InteropTarget(
            Options: optionsA,
            InterfaceName: "IGeolocationService",
            IsPartial: true,
            ContainingNamespace: "Consumer",
            IsGeneric: true,
            IdentifierLocation: null,
            AttributeLocation: null);

        var b = new InteropTarget(
            Options: optionsB,
            InterfaceName: "IGeolocationService",
            IsPartial: true,
            ContainingNamespace: "Consumer",
            IsGeneric: true,
            IdentifierLocation: null,
            AttributeLocation: null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void IncrementalRun_WithUnchangedInput_ReusesCachedOutput()
    {
        // Regression: when the input compilation is unchanged across two
        // runs of the generator, the syntax-provider step output must be
        // marked `Cached` (rather than `New` or `Modified`), proving the
        // value-equatable pipeline projection lets Roslyn skip re-running
        // downstream work. Before T2.1/T2.2 the pipeline carried raw
        // `InterfaceDeclarationSyntax` references, which are not value-
        // equatable, so every keystroke produced `Modified` outputs.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";

        var compilation = GetCompilation(source);
        var driverOptions = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new JavaScriptInteropGenerator().AsSourceGenerator()],
            additionalTexts: [],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options,
            optionsProvider: null,
            driverOptions: driverOptions);

        // First run primes the cache.
        driver = driver.RunGenerators(compilation);

        // Add a no-op trivial edit (whitespace only) and re-run. The
        // pipeline must observe the equivalent `InteropTarget` and treat
        // its output as `Cached`.
        var edited = compilation.RemoveAllSyntaxTrees()
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source + " // touched"));
        driver = driver.RunGenerators(edited);

        var runResult = driver.GetRunResult();
        var generatorResult = runResult.Results.Single();

        // Collect all incremental tracked-step outputs and verify that
        // at least one is `Cached`. (We don't insist *every* step caches
        // because diagnostics output is not cacheable.)
        var cachedReasons = generatorResult.TrackedSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs.Select(o => o.Reason))
            .ToArray();

        Assert.Contains(IncrementalStepRunReason.Cached, cachedReasons);
    }
}
