// Runs the generation pipeline scoped to a focused DOM capability profile.
// Resolves the transitive dependency closure, filters the IR, and produces
// per-profile output + a coverage report.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class ProfilePipeline
{
    public static ProfileGenerationResult Run(
        ProfileDefinition profile,
        IrBundle ir,
        string baseOutputDirectory)
    {
        var symbolIndex = ir.TypescriptSymbols
            .ToDictionary(s => s.Name, StringComparer.Ordinal);

        // Resolve transitive dependencies from the profile's root symbols.
        var closure = TransitiveDependencyResolver.Resolve(profile.RootSymbols, symbolIndex);

        // Separate closure into: symbols that exist in IR vs. names that don't (external refs).
        var includedSymbols = ir.TypescriptSymbols
            .Where(s => closure.Contains(s.Name))
            .OrderBy(s => s.Ordinal)
            .ToList();
        var externalRefs = closure
            .Where(n => !symbolIndex.ContainsKey(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Build a filtered IrBundle containing only the profile's symbols.
        var filteredIr = new IrBundle(
            ir.Manifest,
            includedSymbols,
            ir.WebIdlSymbols); // keep full WebIDL; emitters use it by name lookup

        var outputDir = Path.Combine(
            baseOutputDirectory,
            profile.OutputSubdirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(outputDir);

        // Run the standard pipeline on the filtered IR.
        var result = GenerationPipeline.Run(
            filteredIr,
            outputDir,
            verboseFailures: false);

        // Build per-profile coverage report.
        var coverage = BuildCoverage(
            profile, closure, includedSymbols, externalRefs, result);

        WriteProfileCoverage(outputDir, coverage);

        return new ProfileGenerationResult(profile, includedSymbols.Count,
            closure.Count, externalRefs.Count, result, coverage);
    }

    private static ProfileCoverageReport BuildCoverage(
        ProfileDefinition profile,
        HashSet<string> closure,
        IReadOnlyList<SymbolModel> includedSymbols,
        IReadOnlyList<string> externalRefs,
        GenerationResult pipelineResult)
    {
        return new ProfileCoverageReport(
            ProfileName: profile.Name,
            Description: profile.Description,
            RootSymbols: profile.RootSymbols.ToList(),
            Features: profile.Features.ToList(),
            SecureContext: profile.SecureContext,
            RequiresUserActivation: profile.RequiresUserActivation,
            ClosureSize: closure.Count,
            IncludedSymbolCount: includedSymbols.Count,
            ExternalReferenceCount: externalRefs.Count,
            ExternalReferences: externalRefs,
            Accounting: pipelineResult.Manifest.Accounting,
            Errors: pipelineResult.Errors.Select(e => new ProfileErrorEntry(
                e.SymbolName, e.ExceptionType, e.Message)).ToList(),
            ByteIdentityVerified: false  // profile runs are single-pass; caller can verify
        );
    }

    private static void WriteProfileCoverage(string outputDir, ProfileCoverageReport report)
    {
        var path = Path.Combine(outputDir, "profile-coverage.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
    }
}

// ── Result types ────────────────────────────────────────────────────────────────

public sealed record ProfileGenerationResult(
    ProfileDefinition Profile,
    int IncludedSymbolCount,
    int ClosureSize,
    int ExternalReferenceCount,
    GenerationResult PipelineResult,
    ProfileCoverageReport Coverage);

public sealed record ProfileCoverageReport(
    string ProfileName,
    string Description,
    IReadOnlyList<string> RootSymbols,
    IReadOnlyList<string> Features,
    bool SecureContext,
    bool RequiresUserActivation,
    int ClosureSize,
    int IncludedSymbolCount,
    int ExternalReferenceCount,
    IReadOnlyList<string> ExternalReferences,
    AccountingSummary Accounting,
    IReadOnlyList<ProfileErrorEntry> Errors,
    bool ByteIdentityVerified);

public sealed record ProfileErrorEntry(
    string SymbolName,
    string ExceptionType,
    string Message);
