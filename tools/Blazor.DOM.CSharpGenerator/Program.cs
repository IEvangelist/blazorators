// Blazor.DOM.CSharpGenerator – deterministic C# emitter for Blazor DOM bindings.
// Reads the checked-in JSONL IR, validates hashes, and emits C# source files.
//
// Usage:
//   dotnet run -- --data <path-to-data/Blazor.DOM> --output <output-directory>
//   dotnet run -- --data <path> --output <path> --verify   (regenerate + byte-identity check)
//   dotnet run -- --data <path> --output <path> --profiles <path-to-profiles-dir>
//
// Exit codes:
//   0 = success (zero generation failures, accounting valid, byte-identity passes if --verify)
//   1 = IR or overrides validation failure
//   2 = accounting validation failure
//   3 = byte-identity mismatch (--verify only)
//   4 = generation had failures (generation proceeded but some symbols failed)
//   5 = profile generation had failures

using Blazor.DOM.CSharpGenerator;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Profiles;

var cliArgs = Args.Parse(Environment.GetCommandLineArgs()[1..]);

Console.WriteLine($"Blazor.DOM.CSharpGenerator v{GenerationPipeline.GeneratorVersion}");
Console.WriteLine($"  Data      : {cliArgs.DataDirectory}");
Console.WriteLine($"  Output    : {cliArgs.OutputDirectory}");
Console.WriteLine($"  Verify    : {cliArgs.Verify}");
if (cliArgs.ProfilesDirectory is not null)
    Console.WriteLine($"  Profiles  : {cliArgs.ProfilesDirectory}");
Console.WriteLine();

// ── Step 1: Load and validate the IR ─────────────────────────────────────────
Console.Write("Loading IR...");
IrBundle ir;
try
{
    ir = IrLoader.Load(cliArgs.DataDirectory);
}
catch (IrValidationException ex)
{
    Console.Error.WriteLine($"\nIR validation failed: {ex.Message}");
    return 1;
}
Console.WriteLine(
    $" OK — {ir.TypescriptSymbols.Count} TS symbols, {ir.WebIdlSymbols.Count} WebIDL symbols.");

// ── Step 1b: Load emitter overrides ──────────────────────────────────────────
Console.Write("Loading emitter overrides...");
IReadOnlyDictionary<string, EmitterOverrideEntry> overrides;
try
{
    overrides = EmitterOverridesLoader.Load(cliArgs.DataDirectory);
}
catch (EmitterOverridesException ex)
{
    Console.Error.WriteLine($"\nEmitter overrides validation failed: {ex.Message}");
    return 1;
}
Console.WriteLine($" OK — {overrides.Count} override(s) loaded.");

// ── Step 2: Run full generation pipeline into sibling staging directories ─────
// Sibling staging guarantees that the final directory renames stay on one volume.
var canonicalOutputDirectory = Path.TrimEndingDirectorySeparator(
    Path.GetFullPath(cliArgs.OutputDirectory));
var outputParent = Path.GetDirectoryName(canonicalOutputDirectory)
    ?? throw new InvalidOperationException(
        $"Output directory must have a parent: '{canonicalOutputDirectory}'.");
Directory.CreateDirectory(outputParent);
var stagingDir = Path.Combine(
    outputParent,
    $".{Path.GetFileName(canonicalOutputDirectory)}.generation-{Guid.NewGuid():N}");
var stagingRun1 = Path.Combine(stagingDir, "run1");
var stagingRun2 = Path.Combine(stagingDir, "run2");
int exitCode = 0;

try
{
    Console.Write("Generating (pass 1)...");
    var result1 = GenerationPipeline.Run(
        ir,
        stagingRun1,
        overrides,
        emitHosts: true);

    Console.WriteLine($" done — {result1.WrittenFiles.Count} files.");
    Console.WriteLine($"  Projected         : {result1.Manifest.Accounting.Projected}");
    Console.WriteLine($"    clean           : {result1.Manifest.Accounting.ProjectedClean}");
    Console.WriteLine(
        $"    with deferrals  : {result1.Manifest.Accounting.ProjectedWithDeferredMembers}");
    Console.WriteLine($"  Excluded          : {result1.Manifest.Accounting.Excluded}");
    Console.WriteLine($"  Deferred          : {result1.Manifest.Accounting.Deferred}");
    Console.WriteLine($"  Generation-failed : {result1.Manifest.Accounting.GenerationFailed}");
    Console.WriteLine($"  Total accounted   : {result1.Manifest.Accounting.TotalSymbols} / {ir.TypescriptSymbols.Count}");
    Console.WriteLine($"  Members projected : {result1.Manifest.Accounting.ProjectedMembers}");
    Console.WriteLine($"  Members deferred  : {result1.Manifest.Accounting.DeferredMembers}");
    Console.WriteLine($"  Members failed    : {result1.Manifest.Accounting.FailedMembers}");
    Console.WriteLine(
        $"  Declarations      : {result1.Manifest.Accounting.AccountedSourceDeclarations} / " +
        $"{result1.Manifest.Accounting.SourceDeclarations}");
    Console.WriteLine(
        $"  Members accounted : {result1.Manifest.Accounting.TotalMembers} / " +
        $"{result1.Manifest.Accounting.ExpectedMembers}");
    Console.WriteLine(
        $"  Overloads         : {result1.Manifest.Accounting.AccountedSourceOverloads} / " +
        $"{result1.Manifest.Accounting.SourceOverloads}");
    Console.WriteLine(
        $"  Parameters        : {result1.Manifest.Accounting.AccountedSourceParameters} / " +
        $"{result1.Manifest.Accounting.SourceParameters}");

    // ── Step 3: Accounting validation ────────────────────────────────────────
    if (!result1.Validation.IsValid)
    {
        Console.Error.WriteLine("\nACCOUNTING VALIDATION FAILED:");
        Console.Error.WriteLine(
            $"  Expected {result1.Validation.ExpectedCount} entries, got {result1.Validation.ActualCount}.");
        if (result1.Validation.Duplicates.Count > 0)
            Console.Error.WriteLine($"  Duplicates: {string.Join(", ", result1.Validation.Duplicates)}");
        if (!result1.Validation.MemberReconciliationValid)
        {
            Console.Error.WriteLine(
                $"  Member outcomes: {result1.Validation.ActualMemberCount} / " +
                $"{result1.Validation.ExpectedMemberCount}");
            if (result1.Validation.DuplicateMembers.Count > 0)
            {
                Console.Error.WriteLine(
                    $"  Duplicate members: {string.Join(", ", result1.Validation.DuplicateMembers)}");
            }
        }
        foreach (var diagnostic in result1.Validation.Diagnostics)
            Console.Error.WriteLine($"  {diagnostic}");
        return 2;
    }
    Console.WriteLine(
        "  Accounting: PASS — symbols, declarations, members, overloads, and parameters reconciled.");

    if (result1.Errors.Count > 0)
    {
        Console.Error.WriteLine($"\nGeneration errors ({result1.Errors.Count}):");
        foreach (var e in result1.Errors.Take(40))
            Console.Error.WriteLine($"  [{e.ExceptionType}] {e.SymbolName}: {e.Message.Split('\n')[0]}");
        if (result1.Errors.Count > 40)
            Console.Error.WriteLine($"  ... and {result1.Errors.Count - 40} more (see emitter-manifest.json).");
        exitCode = 4;  // Will report at end; do not return yet — allow verify + profiles
    }

    // ── Step 4 (optional): Byte-identity verification ────────────────────────
    if (cliArgs.Verify)
    {
        Console.WriteLine("\nRunning second generation pass for byte-identity verification...");
        var result2 = GenerationPipeline.Run(
            ir,
            stagingRun2,
            overrides,
            emitHosts: true);

        var scan1 = OutputVerifier.ScanDirectory(stagingRun1);
        var scan2 = OutputVerifier.ScanDirectory(stagingRun2);
        var verification = OutputVerifier.Verify(scan1, scan2);
        if (verification.Identical)
        {
            Console.WriteLine(
                $"  BYTE-IDENTITY: PASS — {scan1.Count} files are identical across both runs.");
        }
        else
        {
            Console.Error.WriteLine("  BYTE-IDENTITY: FAIL");
            foreach (var m in verification.Mismatches)
                Console.Error.WriteLine(
                    $"    MISMATCH: {m.RelativePath}\n      run1={m.Run1Sha256}\n      run2={m.Run2Sha256}");
            foreach (var p in verification.OnlyInRun1)
                Console.Error.WriteLine($"    ONLY-IN-RUN1: {p}");
            foreach (var p in verification.OnlyInRun2)
                Console.Error.WriteLine($"    ONLY-IN-RUN2: {p}");
            return 3;
        }
    }

    // ── Step 5: Atomically replace canonical output (only if no failures) ─────
    // Do NOT write partial canonical output if there are generation failures.
    if (exitCode == 0)
    {
        Console.WriteLine($"\nReplacing canonical output: {cliArgs.OutputDirectory}");
        OutputPromotion.PromoteExhaustive(stagingRun1, canonicalOutputDirectory);
        Console.WriteLine("  Promotion verified.");
    }
    else
    {
        Console.Error.WriteLine(
            "\nSKIPPING canonical output write: generation had failures. " +
            "Fix all generation errors before committing canonical output.");
    }

    // ── Step 6 (optional): Profile generation ────────────────────────────────
    if (cliArgs.ProfilesDirectory is not null)
    {
        Console.WriteLine($"\nGenerating focused profiles from: {cliArgs.ProfilesDirectory}");
        var profiles = ProfileLoader.LoadAll(cliArgs.ProfilesDirectory);
        if (profiles.Count == 0)
        {
            Console.WriteLine("  No *.profile.json files found.");
        }
        else
        {
            var profileFailures = 0;
            foreach (var profile in profiles)
            {
                Console.Write($"  Profile '{profile.Name}'...");
                try
                {
                    var profileResult = ProfilePipeline.Run(profile, ir, cliArgs.OutputDirectory, overrides);
                    var acc = profileResult.PipelineResult.Manifest.Accounting;

                    // Profile fails if: generation errors, accounting invalid,
                    // any pipeline errors (including ByteIdentityException), or byte identity not verified.
                    var profileHasErrors =
                        acc.GenerationFailed > 0
                        || !profileResult.PipelineResult.Validation.IsValid
                        || profileResult.PipelineResult.Errors.Count > 0
                        || !profileResult.Coverage.ByteIdentityVerified;

                    if (profileHasErrors)
                    {
                        Console.Error.WriteLine(
                            $" FAILED — {profileResult.ClosureSize} identities in closure " +
                            $"({profileResult.IncludedSymbolCount} included symbols, " +
                            $"{profileResult.ExternalReferenceCount} external refs), " +
                            $"{acc.GenerationFailed} generation failures, " +
                            $"{profileResult.PipelineResult.Errors.Count} errors, " +
                            $"byteIdentityVerified={profileResult.Coverage.ByteIdentityVerified}.");
                        foreach (var error in profileResult.PipelineResult.Errors.Take(20))
                        {
                            Console.Error.WriteLine(
                                $"    [{error.ExceptionType}] {error.SymbolName}: " +
                                $"{error.Message.Split('\n')[0]}");
                        }
                        profileFailures++;
                    }
                    else
                    {
                        Console.WriteLine(
                            $" OK — {profileResult.ClosureSize} identities in closure " +
                            $"({profileResult.IncludedSymbolCount} included symbols, " +
                            $"{profileResult.ExternalReferenceCount} external refs), " +
                            $"{acc.Projected} projected, 0 failures.");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($" FAILED: {ex.Message}");
                    profileFailures++;
                }
            }

            if (profileFailures > 0)
            {
                Console.Error.WriteLine($"\n{profileFailures} profile(s) failed.");
                if (exitCode == 0) exitCode = 5;
            }
        }
    }
}
finally
{
    OutputPromotion.DeleteDirectoryWithRetry(stagingDir);
}

if (exitCode != 0)
{
    Console.Error.WriteLine($"\nExiting with code {exitCode} (generation had failures).");
    return exitCode;
}

Console.WriteLine("\nDone.");
return 0;

// ── Args ──────────────────────────────────────────────────────────────────────

internal sealed record Args(
    string DataDirectory,
    string OutputDirectory,
    bool Verify,
    string? ProfilesDirectory)
{
    public static Args Parse(string[] args)
    {
        string? data = null, output = null, profiles = null;
        var verify = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data" when i + 1 < args.Length:
                    data = args[++i]; break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i]; break;
                case "--profiles" when i + 1 < args.Length:
                    profiles = args[++i]; break;
                case "--verify":
                    verify = true; break;
            }
        }

        // Default to checked-in data path relative to the solution root
        data ??= ResolveDefault("data", "Blazor.DOM");
        output ??= ResolveDefault("data", "Blazor.DOM.Generated");

        return new Args(data, output, verify, profiles);
    }

    private static string ResolveDefault(params string[] parts)
    {
        // Walk up until we find the solution root (contains blazorators.sln)
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "blazorators.sln")))
                return Path.Combine([dir, .. parts]);
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        return Path.Combine([Directory.GetCurrentDirectory(), .. parts]);
    }
}
