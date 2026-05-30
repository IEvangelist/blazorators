// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Corpus-coverage harness for Phase A of the universal TS-to-C# coverage
/// effort.
///
/// <para>
/// The intent is "no TS shape we are pointed at should ever crash the
/// parser". Today the generator is exercised against ~30 hand-picked
/// interfaces in the rest of the test suite, leaving 1000+ DOM interfaces
/// and 700+ type aliases unobserved. This harness drives every entry in
/// the bundled <c>lib.dom.d.ts</c> through the parser and asserts:
/// </para>
///
/// <list type="number">
///   <item><description>
///     No exception escapes <see cref="TypeDeclarationParser.ToObject"/> for
///     any of the ~1000 interfaces.
///   </description></item>
///   <item><description>
///     No exception escapes <see cref="TypeDeclarationParser.ParseTargetType"/>
///     either (the top-level service-projection path that real consumers
///     exercise).
///   </description></item>
///   <item><description>
///     A measurable fraction projects to a non-null <see cref="CSharpObject"/>
///     -- the "coverage ratio" baseline. We start with a permissive floor and
///     ratchet it up as Phase B lands TS-construct fixes.
///   </description></item>
/// </list>
///
/// <para>
/// Failures and skipped types are written to
/// <c>artifacts/coverage/lib-dom-coverage.txt</c> on every run so a
/// human reviewer can see exactly which TS shapes regressed.
/// </para>
/// </summary>
public class CorpusCoverageTests
{
    private readonly ITestOutputHelper _output;

    public CorpusCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Coverage floor for the dependent-type pass (<see cref="TypeDeclarationParser.ToObject"/>).
    /// Today's parser already succeeds on the overwhelming majority of DOM
    /// interfaces; this floor is intentionally generous to avoid flapping
    /// when MDN ships new types and to leave headroom for Phase B.
    /// Ratchet up as construct support lands.
    /// </summary>
    private const double InterfaceCoverageFloor = 0.95d;

    /// <summary>
    /// Coverage floor for the top-level pass (<see cref="TypeDeclarationParser.ParseTargetType"/>).
    /// Same rationale as <see cref="InterfaceCoverageFloor"/>.
    /// </summary>
    private const double TopLevelCoverageFloor = 0.95d;

    [Fact]
    public void Interface_Corpus_NeverThrows_AndMeetsCoverageFloor()
    {
        var reader = TypeDeclarationReader.Default;
        Assert.True(reader.IsInitialized, "Embedded lib.dom.d.ts reader failed to initialize.");
        Assert.True(reader.DeclarationCount > 500,
            $"Expected at least 500 interface declarations in lib.dom.d.ts; got {reader.DeclarationCount}.");

        var parser = new TypeDeclarationParser(reader);
        var failures = new List<CoverageFailure>();
        var projected = 0;
        var nullProjections = 0;

        foreach (var name in reader.DeclarationNames)
        {
            if (!reader.TryGetDeclaration(name, out var declarationText) || declarationText is null)
            {
                failures.Add(new CoverageFailure(name, "Missing declaration text after lookup."));
                continue;
            }

            try
            {
                var obj = parser.ToObject(declarationText);
                if (obj is null)
                {
                    nullProjections++;
                }
                else
                {
                    projected++;
                }
            }
            catch (Exception ex)
            {
                failures.Add(new CoverageFailure(name, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        WriteCoverageReport(
            scenario: "Interface_Corpus",
            totalCount: reader.DeclarationCount,
            projectedCount: projected,
            nullCount: nullProjections,
            failures: failures);

        Assert.Empty(failures);

        var coverage = projected / (double)reader.DeclarationCount;
        Assert.True(coverage >= InterfaceCoverageFloor,
            $"Interface coverage ratio {coverage:P2} is below floor {InterfaceCoverageFloor:P2} " +
            $"({projected}/{reader.DeclarationCount} projected, {nullProjections} null).");
    }

    [Fact]
    public void TopLevel_Corpus_NeverThrows_AndMeetsCoverageFloor()
    {
        var reader = TypeDeclarationReader.Default;
        Assert.True(reader.IsInitialized, "Embedded lib.dom.d.ts reader failed to initialize.");

        var parser = new TypeDeclarationParser(reader);
        var failures = new List<CoverageFailure>();
        var projected = 0;
        var unprojected = 0;

        foreach (var name in reader.DeclarationNames)
        {
            ParserResult<CSharpTopLevelObject> result;
            try
            {
                result = parser.ParseTargetType(name);
            }
            catch (Exception ex)
            {
                failures.Add(new CoverageFailure(name, $"{ex.GetType().Name}: {ex.Message}"));
                continue;
            }

            switch (result.Status)
            {
                case ParserResultStatus.SuccessfullyParsed:
                    if (result.Value is null)
                    {
                        unprojected++;
                    }
                    else
                    {
                        projected++;
                    }
                    break;

                case ParserResultStatus.ErrorParsing:
                    failures.Add(new CoverageFailure(name, $"ErrorParsing: {result.Error}"));
                    break;

                case ParserResultStatus.TargetTypeNotFound:
                    // The name came directly from the reader, so this branch
                    // means the parser disagrees with the reader about what
                    // counts as a declaration -- a bug. Surface it as a
                    // failure so we notice.
                    failures.Add(new CoverageFailure(name, "Reader has declaration but ParseTargetType says TargetTypeNotFound."));
                    break;

                default:
                    failures.Add(new CoverageFailure(name, $"Unexpected status: {result.Status}."));
                    break;
            }
        }

        WriteCoverageReport(
            scenario: "TopLevel_Corpus",
            totalCount: reader.DeclarationCount,
            projectedCount: projected,
            nullCount: unprojected,
            failures: failures);

        Assert.Empty(failures);

        var coverage = projected / (double)reader.DeclarationCount;
        Assert.True(coverage >= TopLevelCoverageFloor,
            $"Top-level coverage ratio {coverage:P2} is below floor {TopLevelCoverageFloor:P2} " +
            $"({projected}/{reader.DeclarationCount} projected, {unprojected} null).");
    }

    [Fact]
    public void TypeAlias_Corpus_NeverThrows_OnLookup()
    {
        var reader = TypeDeclarationReader.Default;
        Assert.True(reader.IsInitialized, "Embedded lib.dom.d.ts reader failed to initialize.");
        Assert.True(reader.TypeAliasCount > 100,
            $"Expected at least 100 type aliases in lib.dom.d.ts; got {reader.TypeAliasCount}.");

        // Phase A only proves lookup is stable. Projection of aliases as
        // C# types (enums, delegates, etc.) is Phase B/C work. This test
        // guards the reader's alias map against silent regression and
        // proves we can still see every alias the source defines.
        var failures = new List<CoverageFailure>();
        foreach (var name in reader.TypeAliasNames)
        {
            try
            {
                if (!reader.TryGetTypeAlias(name, out var aliasText) || string.IsNullOrWhiteSpace(aliasText))
                {
                    failures.Add(new CoverageFailure(name, "Alias enumerable returned name but TryGetTypeAlias gave no text."));
                }
            }
            catch (Exception ex)
            {
                failures.Add(new CoverageFailure(name, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        WriteCoverageReport(
            scenario: "TypeAlias_Corpus",
            totalCount: reader.TypeAliasCount,
            projectedCount: reader.TypeAliasCount - failures.Count,
            nullCount: 0,
            failures: failures);

        Assert.Empty(failures);
    }

    private void WriteCoverageReport(
        string scenario,
        int totalCount,
        int projectedCount,
        int nullCount,
        IReadOnlyCollection<CoverageFailure> failures)
    {
        var coverageDir = Path.Combine(AppContext.BaseDirectory, "coverage");
        Directory.CreateDirectory(coverageDir);

        var reportPath = Path.Combine(coverageDir, $"lib-dom-{scenario}.txt");
        var coverage = totalCount == 0 ? 0d : projectedCount / (double)totalCount;

        using var writer = new StreamWriter(reportPath, append: false);
        writer.WriteLine($"# {scenario}");
        writer.WriteLine($"# Generated at {DateTimeOffset.UtcNow:O}");
        writer.WriteLine($"Total       : {totalCount}");
        writer.WriteLine($"Projected   : {projectedCount}");
        writer.WriteLine($"NullResult  : {nullCount}");
        writer.WriteLine($"Failures    : {failures.Count}");
        writer.WriteLine($"Coverage    : {coverage:P2}");
        writer.WriteLine();
        if (failures.Count > 0)
        {
            writer.WriteLine("## Failures");
            foreach (var f in failures)
            {
                writer.WriteLine($"- {f.Name}: {f.Reason}");
            }
        }

        _output.WriteLine($"[{scenario}] {projectedCount}/{totalCount} projected ({coverage:P2}), {failures.Count} failures. Report: {reportPath}");
    }

    private readonly record struct CoverageFailure(string Name, string Reason);
}
