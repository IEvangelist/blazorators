// Blazor.DOM.CSharpGenerator – deterministic C# emitter for Blazor DOM bindings.
// Reads the checked-in JSONL IR, validates hashes, and emits C# source files.
//
// Usage:
//   dotnet run -- --data <path-to-data/Blazor.DOM> --output <output-directory>
//   dotnet run -- --data <path> --output <path> --verify   (regenerate + byte-identity check)
//   dotnet run -- --data <path> --output <path> --profiles <path-to-profiles-dir>

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

// ── Step 2: Run full generation pipeline ─────────────────────────────────────
Console.Write("Generating...");
var run1Output = Path.Combine(cliArgs.OutputDirectory, "run1");
var result1 = GenerationPipeline.Run(ir, run1Output);

Console.WriteLine($" OK — {result1.WrittenFiles.Count} files written.");
Console.WriteLine($"  Projected         : {result1.Manifest.Accounting.Projected}");
Console.WriteLine($"  Excluded          : {result1.Manifest.Accounting.Excluded}");
Console.WriteLine($"  Deferred          : {result1.Manifest.Accounting.Deferred}");
Console.WriteLine($"  Generation-failed : {result1.Manifest.Accounting.GenerationFailed}");
Console.WriteLine($"  Total accounted   : {result1.Manifest.Accounting.TotalSymbols} / {ir.TypescriptSymbols.Count}");

// ── Step 3: Accounting validation ─────────────────────────────────────────────
if (!result1.Validation.IsValid)
{
    Console.Error.WriteLine("\nACCOUNTING VALIDATION FAILED:");
    Console.Error.WriteLine(
        $"  Expected {result1.Validation.ExpectedCount} entries, got {result1.Validation.ActualCount}.");
    if (result1.Validation.Duplicates.Count > 0)
        Console.Error.WriteLine($"  Duplicates: {string.Join(", ", result1.Validation.Duplicates)}");
    return 2;
}
Console.WriteLine("  Accounting: PASS — all symbols accounted.");

if (result1.Errors.Count > 0)
{
    Console.WriteLine($"\nGeneration errors ({result1.Errors.Count}):");
    foreach (var e in result1.Errors.Take(20))
        Console.WriteLine($"  [{e.ExceptionType}] {e.SymbolName}: {e.Message.Split('\n')[0]}");
    if (result1.Errors.Count > 20)
        Console.WriteLine($"  ... and {result1.Errors.Count - 20} more (see emitter-manifest.json).");
}

// ── Step 4 (optional): Byte-identity verification ─────────────────────────────
if (cliArgs.Verify)
{
    Console.WriteLine("\nRunning second generation pass for byte-identity verification...");
    var run2Output = Path.Combine(cliArgs.OutputDirectory, "run2");
    var result2 = GenerationPipeline.Run(ir, run2Output);

    var verification = OutputVerifier.Verify(result1.WrittenFiles, result2.WrittenFiles);
    if (verification.Identical)
    {
        Console.WriteLine(
            $"  BYTE-IDENTITY: PASS — {result1.WrittenFiles.Count} files are identical across both runs.");
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

    // Copy run1 to the canonical output location
    CopyDirectory(run1Output, cliArgs.OutputDirectory, overwrite: true, skipRunDirs: true);
}
else
{
    // Copy run1 to canonical output
    CopyDirectory(run1Output, cliArgs.OutputDirectory, overwrite: true, skipRunDirs: false);
}

// ── Step 5 (optional): Profile generation ────────────────────────────────────
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
        foreach (var profile in profiles)
        {
            Console.Write($"  Profile '{profile.Name}'...");
            try
            {
                var profileResult = ProfilePipeline.Run(profile, ir, cliArgs.OutputDirectory);
                var acc = profileResult.PipelineResult.Manifest.Accounting;
                Console.WriteLine(
                    $" OK — {profileResult.IncludedSymbolCount} symbols in closure " +
                    $"({profileResult.ExternalReferenceCount} external refs), " +
                    $"{acc.Projected} projected, {acc.GenerationFailed} failed.");
                if (!profileResult.PipelineResult.Validation.IsValid)
                {
                    Console.Error.WriteLine(
                        $"    ACCOUNTING FAIL: {profileResult.PipelineResult.Validation.ActualCount}" +
                        $" / {profileResult.PipelineResult.Validation.ExpectedCount}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($" FAILED: {ex.Message}");
            }
        }
    }
}

Console.WriteLine("\nDone.");
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static void CopyDirectory(string src, string dst, bool overwrite, bool skipRunDirs)
{
    foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(src, file);
        if (skipRunDirs && (rel.StartsWith("run1") || rel.StartsWith("run2"))) continue;
        var target = Path.Combine(dst, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite);
    }
}

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
