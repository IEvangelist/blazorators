// Runs the generation pipeline scoped to a focused DOM capability profile.
// Resolves the transitive dependency closure, filters the IR, and produces
// per-profile output + a coverage report.
// FAIL-CLOSED: profile output is only written to canonical when all members project cleanly
// AND byte-identity is proven across two isolated generation passes.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Hosts;
using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class ProfilePipeline
{
    private static readonly IReadOnlySet<string> SupportedAmbientReferences =
        new HashSet<string>(
        [
            "ArrayBuffer",
            "ArrayBufferView",
            "Array",
            "AsyncIteratorObject",
            "BuiltinIteratorReturn",
            "Date",
            "Error",
            "Exclude",
            "Iterable",
            "Promise",
            "ReadonlyArray",
            "Record",
            "Uint8Array",
        ],
        StringComparer.Ordinal);

    public static ProfileGenerationResult Run(
        ProfileDefinition profile,
        IrBundle ir,
        string baseOutputDirectory,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null,
        Action<OutputPromotionFailurePoint>? promotionFailureInjector = null,
        Action<Exception>? cleanupFailureHandler = null)
    {
        var canonicalOutputDir = ProfileOutputPath.Resolve(
            baseOutputDirectory,
            profile.OutputSubdirectory);

        var sourceIndex = ir.TypescriptSymbols
            .ToDictionary(s => s.Name, StringComparer.Ordinal);
        var generationIndex = profile.MinimalDependencyContracts
            ? sourceIndex.ToDictionary(
                pair => pair.Key,
                pair => ApplyTransportOverrides(
                    SelectProfileMembers(pair.Value, profile),
                    profile),
                StringComparer.Ordinal)
            : sourceIndex;
        ValidateReviewedExclusions(profile, sourceIndex, generationIndex);

        var closure = TransitiveDependencyResolver.Resolve(
            profile.RootSymbols,
            generationIndex);

        var includedSymbols = generationIndex.Values
            .Where(symbol => closure.Contains(symbol.Name))
            .OrderBy(s => s.Ordinal)
            .ToList();
        var externalRefs = closure
            .Where(n => !sourceIndex.ContainsKey(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        HostPackageOptions? hostOptions = null;
        if (profile.EntryPoints is not null)
        {
            ValidatePackageClosure(profile, sourceIndex, closure, externalRefs);
            hostOptions = new HostPackageOptions(new HostCapabilityMetadata(
                profile.Name,
                profile.Description,
                profile.Features,
                profile.SecureContext,
                profile.RequiresUserActivation,
                profile.EntryPoints,
                profile.Permissions));
        }

        var filteredIr = new IrBundle(
            ir.Manifest,
            includedSymbols,
            ir.WebIdlSymbols);

        // Sibling staging keeps candidate promotion and rollback on one volume.
        var profileParent = Path.GetDirectoryName(canonicalOutputDir)
            ?? throw new InvalidOperationException(
                $"Profile output must have a parent: '{canonicalOutputDir}'.");
        Directory.CreateDirectory(profileParent);
        var stagingBase = Path.Combine(
            profileParent,
            $".{Path.GetFileName(canonicalOutputDir)}.generation-{Guid.NewGuid():N}");
        var stagingPass1 = Path.Combine(stagingBase, "pass1");
        var stagingPass2 = Path.Combine(stagingBase, "pass2");

        GenerationResult result;
        ProfileCoverageReport coverage;

        try
        {
            // ── Pass 1 ──────────────────────────────────────────────────────
            result = GenerationPipeline.Run(
                filteredIr,
                stagingPass1,
                overrides,
                verboseFailures: false,
                emitHosts: hostOptions is not null,
                hostPackageOptions: hostOptions);
            if (hostOptions is not null)
                ValidateSupportedTransports(profile, stagingPass1);

            coverage = BuildCoverage(
                profile, closure, includedSymbols, externalRefs, result,
                byteIdentityVerified: false);

            if (result.Errors.Count > 0 || !result.Validation.IsValid)
            {
                // Profile has failures — do NOT write to canonical.
                return new ProfileGenerationResult(
                    profile, includedSymbols.Count,
                    closure.Count, externalRefs.Count, result, coverage);
            }

            // ── Pass 2 (byte-identity verification) ─────────────────────────
            var result2 = GenerationPipeline.Run(
                filteredIr,
                stagingPass2,
                overrides,
                verboseFailures: false,
                emitHosts: hostOptions is not null,
                hostPackageOptions: hostOptions);
            if (hostOptions is not null)
                ValidateSupportedTransports(profile, stagingPass2);

            var coverage2 = BuildCoverage(
                profile, closure, includedSymbols, externalRefs, result2,
                byteIdentityVerified: false);

            WriteProfileCoverage(stagingPass1, coverage);
            WriteProfileCoverage(stagingPass2, coverage2);

            var files1 = ScanAllFiles(stagingPass1);
            var files2 = ScanAllFiles(stagingPass2);
            var verification = OutputVerifier.Verify(files1, files2);

            if (!verification.Identical)
            {
                // Nondeterminism detected — refuse canonical write
                var details = $"{verification.Mismatches.Count} file mismatch(es) between pass1 and pass2";
                var biError = new GenerationError("profile-coverage",
                    $"BYTE-IDENTITY FAIL for profile '{profile.Name}': {details}. " +
                    "Generation is nondeterministic — canonical output not written.",
                    "ByteIdentityException");
                var biResult = result with
                {
                    Errors = [.. result.Errors, biError]
                };
                return new ProfileGenerationResult(
                    profile, includedSymbols.Count,
                    closure.Count, externalRefs.Count, biResult,
                    coverage with { ByteIdentityVerified = false });
            }

            var verifiedCoverage = coverage with { ByteIdentityVerified = true };
            var verifiedCoverage2 = coverage2 with { ByteIdentityVerified = true };
            WriteProfileCoverage(stagingPass1, verifiedCoverage);
            WriteProfileCoverage(stagingPass2, verifiedCoverage2);

            var files1Final = ScanAllFiles(stagingPass1);
            var files2Final = ScanAllFiles(stagingPass2);
            var finalVerification = OutputVerifier.Verify(files1Final, files2Final);

            if (!finalVerification.Identical)
            {
                var details = $"{finalVerification.Mismatches.Count} file mismatch(es) between pass1 and pass2";
                var biError = new GenerationError("profile-coverage",
                    $"BYTE-IDENTITY FAIL (final verification pass) for profile '{profile.Name}': {details}. " +
                    "Generation is nondeterministic — canonical output not written.",
                    "ByteIdentityException");
                var biResult = result with
                {
                    Errors = [.. result.Errors, biError]
                };
                return new ProfileGenerationResult(
                    profile, includedSymbols.Count,
                    closure.Count, externalRefs.Count, biResult,
                    coverage with { ByteIdentityVerified = false });
            }

            // ── Byte-identity PASS: promote pass1 to canonical ──────────────
            OutputPromotion.PromoteProfile(
                stagingPass1,
                canonicalOutputDir,
                promotionFailureInjector,
                cleanupFailureHandler is null
                    ? null
                    : exception => cleanupFailureHandler(exception));

            return new ProfileGenerationResult(profile, includedSymbols.Count,
                closure.Count, externalRefs.Count, result, verifiedCoverage);
        }
        finally
        {
            if (cleanupFailureHandler is null)
            {
                OutputPromotion.DeleteDirectoryWithRetry(stagingBase);
            }
            else
            {
                try
                {
                    OutputPromotion.DeleteDirectoryWithRetry(stagingBase);
                }
                catch (Exception exception)
                {
                    cleanupFailureHandler(exception);
                }
            }
        }
    }

    /// <summary>
    /// Scans a staging directory for all generated files (C# source AND emitter-manifest.json)
    /// for byte-identity comparison across two passes.
    /// </summary>
    private static IReadOnlyList<GeneratedFile> ScanAllFiles(string directory)
        => OutputVerifier.ScanDirectory(directory);

    private static SymbolModel SelectProfileMembers(
        SymbolModel symbol,
        ProfileDefinition profile)
    {
        if (profile.MemberIncludes?.TryGetValue(
                symbol.Name,
                out var includes) == true)
        {
            var includeAll = includes.Contains("*", StringComparer.Ordinal);
            var names = includes.ToHashSet(StringComparer.Ordinal);
            return symbol with
            {
                Declarations = symbol.Declarations
                    .Select(declaration =>
                        declaration with
                        {
                            Members = includeAll
                                ? declaration.Members
                                : declaration.Members
                                    .Where(member =>
                                    {
                                        var memberName = member.Name?.Text
                                            ?? $"${member.Kind}";
                                        return names.Contains(memberName)
                                            || names.Contains(
                                                $"{memberName}@{declaration.Ordinal}/{member.Ordinal}");
                                    })
                                    .ToList(),
                        })
                    .ToList(),
            };
        }

        if (profile.RootSymbols.Contains(symbol.Name, StringComparer.Ordinal))
            return symbol;

        var classification = symbol.Semantic.Classifications.FirstOrDefault();
        if (classification is "enum")
            return symbol;

        return symbol with
        {
            Declarations = symbol.Declarations
                .Select(declaration => declaration with
                {
                    Members = [],
                    NamespaceMembers = [],
                })
                .ToList(),
        };
    }

    private static SymbolModel ApplyTransportOverrides(
        SymbolModel symbol,
        ProfileDefinition profile)
    {
        var overrides = (profile.TransportOverrides ?? [])
            .Where(item => string.Equals(
                item.Symbol,
                symbol.Name,
                StringComparison.Ordinal))
            .ToDictionary(item => item.Member, StringComparer.Ordinal);
        if (overrides.Count == 0)
            return symbol;

        var matched = new HashSet<string>(StringComparer.Ordinal);
        var declarations = symbol.Declarations
            .Select(declaration => declaration with
            {
                Members = declaration.Members
                    .Select(member =>
                    {
                        var memberName = member.Name?.Text
                            ?? $"${member.Kind}";
                        if (!overrides.TryGetValue(memberName, out var transportOverride))
                            return member;

                        var endpoint = member.ReturnType ?? member.Type
                            ?? throw new InvalidDataException(
                                $"Transport override '{symbol.Name}.{memberName}' " +
                                "does not target a value endpoint.");
                        matched.Add(memberName);
                        var transport = new TransportModel(
                            transportOverride.Kind,
                            endpoint.Transport?.Nullable == true,
                            endpoint.Transport?.SourceType
                                ?? endpoint.CheckerType
                                ?? memberName,
                            false,
                            true,
                            null);
                        return member.ReturnType is not null
                            ? member with
                            {
                                ReturnType = member.ReturnType with
                                {
                                    Transport = transport,
                                },
                            }
                            : member with
                            {
                                Type = member.Type! with
                                {
                                    Transport = transport,
                                },
                            };
                    })
                    .ToList(),
            })
            .ToList();

        var missing = overrides.Keys
            .Where(member => !matched.Contains(member))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' transport override(s) did not " +
                $"match selected member(s): {string.Join(", ", missing.Select(
                    member => $"{symbol.Name}.{member}"))}");
        }
        return symbol with { Declarations = declarations };
    }

    private static void ValidatePackageClosure(
        ProfileDefinition profile,
        IReadOnlyDictionary<string, SymbolModel> sourceIndex,
        IReadOnlySet<string> closure,
        IReadOnlyList<string> externalReferences)
    {
        var missingRoots = profile.RootSymbols
            .Where(root => !sourceIndex.ContainsKey(root))
            .ToList();
        if (missingRoots.Count > 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' has missing root symbol(s): " +
                string.Join(", ", missingRoots));
        }

        var ambiguousRoots = profile.RootSymbols
            .Where(root => string.Equals(
                sourceIndex[root].Semantic.Status,
                "ambiguous",
                StringComparison.Ordinal))
            .ToList();
        if (ambiguousRoots.Count > 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' has ambiguous root symbol(s): " +
                string.Join(", ", ambiguousRoots));
        }

        var leakedReferences = externalReferences
            .Where(reference => !SupportedAmbientReferences.Contains(reference))
            .ToList();
        if (leakedReferences.Count > 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' closure leaks unresolved " +
                $"reference(s): {string.Join(", ", leakedReferences)}");
        }

        foreach (var entryPoint in profile.EntryPoints!)
        {
            if (!closure.Contains(entryPoint.Symbol))
            {
                throw new InvalidDataException(
                    $"Entry point '{entryPoint.Name}' is outside the resolved closure.");
            }
        }
    }

    private static void ValidateReviewedExclusions(
        ProfileDefinition profile,
        IReadOnlyDictionary<string, SymbolModel> sourceIndex,
        IReadOnlyDictionary<string, SymbolModel> generationIndex)
    {
        foreach (var exclusion in profile.ReviewedExclusions ?? [])
        {
            if (!sourceIndex.TryGetValue(exclusion.Symbol, out var source))
            {
                throw new InvalidDataException(
                    $"Package profile '{profile.Name}' reviewed exclusion references " +
                    $"missing symbol '{exclusion.Symbol}'.");
            }

            var sourceMatches = MatchingMembers(source, exclusion.Member).ToList();
            if (sourceMatches.Count == 0)
            {
                throw new InvalidDataException(
                    $"Package profile '{profile.Name}' reviewed exclusion did not match " +
                    $"'{exclusion.Symbol}.{exclusion.Member}'.");
            }

            if (generationIndex.TryGetValue(exclusion.Symbol, out var selected)
                && MatchingMembers(selected, exclusion.Member).Any())
            {
                throw new InvalidDataException(
                    $"Package profile '{profile.Name}' reviewed exclusion " +
                    $"'{exclusion.Symbol}.{exclusion.Member}' is still selected.");
            }
        }

        static IEnumerable<MemberModel> MatchingMembers(
            SymbolModel symbol,
            string memberIdentity)
        {
            foreach (var declaration in symbol.Declarations)
            {
                foreach (var member in declaration.Members)
                {
                    var name = member.Name?.Text ?? $"${member.Kind}";
                    if (string.Equals(memberIdentity, name, StringComparison.Ordinal)
                        || string.Equals(
                            memberIdentity,
                            $"{name}@{declaration.Ordinal}/{member.Ordinal}",
                            StringComparison.Ordinal))
                    {
                        yield return member;
                    }
                }
            }
        }
    }

    private static void ValidateSupportedTransports(
        ProfileDefinition profile,
        string outputDirectory)
    {
        var unsupportedFiles = new[] { "Server", "WebAssembly" }
            .SelectMany(hostDirectory =>
            {
                var path = Path.Combine(outputDirectory, hostDirectory);
                return Directory.Exists(path)
                    ? Directory.EnumerateFiles(
                        path,
                        "*.cs",
                        SearchOption.AllDirectories)
                    : Array.Empty<string>();
            })
            .Where(path => File.ReadAllText(path).Contains(
                "DomTransportKind.Unsupported",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(outputDirectory, path))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (unsupportedFiles.Count > 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' emits unsupported transport in: " +
                string.Join(", ", unsupportedFiles));
        }
    }

    private static string SerializeCoverage(ProfileCoverageReport report)
        => JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

    private static ProfileCoverageReport BuildCoverage(
        ProfileDefinition profile,
        HashSet<string> closure,
        IReadOnlyList<SymbolModel> includedSymbols,
        IReadOnlyList<string> externalRefs,
        GenerationResult pipelineResult,
        bool byteIdentityVerified)
    {
        return new ProfileCoverageReport(
            ProfileName: profile.Name,
            Description: profile.Description,
            RootSymbols: profile.RootSymbols.ToList(),
            Features: profile.Features.ToList(),
            Permissions: profile.Permissions?.ToList() ?? [],
            SecureContext: profile.SecureContext,
            RequiresUserActivation: profile.RequiresUserActivation,
            ClosureSize: closure.Count,
            IncludedSymbolCount: includedSymbols.Count,
            ExternalReferenceCount: externalRefs.Count,
            ExternalReferences: externalRefs,
            Accounting: pipelineResult.Manifest.Accounting,
            TransportOverrides: profile.TransportOverrides ?? [],
            Errors: pipelineResult.Errors.Select(e => new ProfileErrorEntry(
                e.SymbolName, e.ExceptionType, e.Message)).ToList(),
            ByteIdentityVerified: byteIdentityVerified,
            ReviewedExclusions: profile.ReviewedExclusions ?? []
        );
    }

    private static void WriteProfileCoverage(string outputDir, ProfileCoverageReport report)
    {
        var path = Path.Combine(outputDir, "profile-coverage.json");
        var json = SerializeCoverage(report).Replace("\r\n", "\n").Replace('\r', '\n');
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
    IReadOnlyList<string> Permissions,
    bool SecureContext,
    bool RequiresUserActivation,
    int ClosureSize,
    int IncludedSymbolCount,
    int ExternalReferenceCount,
    IReadOnlyList<string> ExternalReferences,
    AccountingSummary Accounting,
    IReadOnlyList<ProfileTransportOverride> TransportOverrides,
    IReadOnlyList<ProfileErrorEntry> Errors,
    bool ByteIdentityVerified,
    IReadOnlyList<ProfileReviewedExclusion> ReviewedExclusions);

public sealed record ProfileErrorEntry(
    string SymbolName,
    string ExceptionType,
    string Message);
