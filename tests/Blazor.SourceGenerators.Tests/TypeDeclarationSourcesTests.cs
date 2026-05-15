// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// T5.1 regression coverage. Before this work,
/// <c>JSAutoInteropAttribute.TypeDeclarationSources</c> was a documented
/// option that did nothing - the reader always loaded the embedded
/// <c>lib.dom.d.ts</c> regardless. Consumers wanting to wire in a
/// non-DOM TypeScript declaration file (their own ambient module,
/// third-party <c>.d.ts</c>, etc.) had no way to do it.
///
/// The fix wires the analyzer's <c>AdditionalTextsProvider</c> into the
/// pipeline so that <c>AdditionalFiles</c> entries ending in <c>.d.ts</c>
/// are made available to the generator. When a consumer sets
/// <c>TypeDeclarationSources = new[] { "my.d.ts" }</c>, the generator
/// matches the entry against the additional-files set by basename and
/// parses the supplied content instead of (or in addition to) the
/// embedded DOM declarations.
/// </summary>
public class TypeDeclarationSourcesTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    /// <summary>
    /// Minimal hand-rolled <c>.d.ts</c> declaring a custom interop type.
    /// Stays small on purpose - the parser tests in <c>TypeDeclarationParserTests</c>
    /// already cover the parser surface; here we just want to prove that
    /// the additional-text content reaches the parser at all.
    /// </summary>
    private const string CustomDts = @"
interface CustomCalculator {
    add(a: number, b: number): number;
    multiply(a: number, b: number): number;
}
";

    [Fact]
    public void TypeDeclarationReader_FromText_ParsesInterface()
    {
        // Sanity check that the new text-based constructor actually populates
        // the declaration map. If this fails, the AdditionalFiles ingestion
        // path cannot possibly work either.
        var reader = new Blazor.SourceGenerators.Readers.TypeDeclarationReader(CustomDts);
        Assert.True(reader.TryGetDeclaration("CustomCalculator", out _));
    }

    [Fact]
    public void AdditionalFile_DTS_IsIngestedAndUsedByParser()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""CustomCalculator"",
        Implementation = ""window.customCalculator"",
        TypeDeclarationSources = new[] { ""custom.d.ts"" })]
    public partial interface ICustomCalculatorService { }
}";

        var additionalTexts = new[]
        {
            new InMemoryAdditionalText("custom.d.ts", CustomDts),
        };

        var result = GetRunResult(source, additionalTexts);

        // The implementation file is only emitted if the parser successfully
        // resolved the `CustomCalculator` interface from the additional text.
        Assert.Contains(
            result.GeneratedTrees,
            t => System.IO.Path.GetFileName(t.FilePath) == "CustomCalculatorService.g.cs");
    }

    [Fact]
    public void MissingAdditionalFile_ReportsTargetTypeNotFound()
    {
        // Without an AdditionalFile to back it, a custom TypeName cannot
        // resolve and the generator should now surface the standard
        // TargetTypeNotFound diagnostic (BR0006) instead of silently
        // succeeding with no output.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""CustomCalculator"",
        Implementation = ""window.customCalculator"",
        TypeDeclarationSources = new[] { ""missing.d.ts"" })]
    public partial interface ICustomCalculatorService { }
}";

        var compilation = GetCompilation(source);
        var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver
            .Create(SourceGenerators)
            .RunGenerators(compilation);

        var diagnostics = driver.GetRunResult().Diagnostics;

        Assert.Contains(diagnostics, d => d.Id == "BR0006");
    }
}
