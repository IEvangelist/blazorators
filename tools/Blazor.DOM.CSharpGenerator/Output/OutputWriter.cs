// Deterministic output writer and byte-identity verifier.
// Files are named by C# type name (stable, no timestamps, no checkout-relative paths).
// OutputVerifier computes a sha256 of each file and can prove two runs produce identical output.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.Output;

public sealed class OutputWriter
{
    private readonly string _outputDirectory;
    private readonly List<GeneratedFile> _written = [];
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sources =
        new(StringComparer.Ordinal);

    public IReadOnlyList<GeneratedFile> WrittenFiles => _written;

    public OutputWriter(string outputDirectory)
    {
        _outputDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(outputDirectory));
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Writes a generated C# source file. Filename is derived deterministically
    /// from the C# type name. Returns the relative path written.
    /// </summary>
    public string Write(string csharpTypeName, string source, string subdirectory = "")
    {
        ValidateFileStem(csharpTypeName);
        var relativeDirectory = ValidateRelativePath(subdirectory, nameof(subdirectory));
        var dir = string.IsNullOrEmpty(subdirectory)
            ? _outputDirectory
            : ResolveOwnedPath(relativeDirectory);
        Directory.CreateDirectory(dir);

        var fileName = $"{csharpTypeName}.g.cs";
        var fullPath = Path.Combine(dir, fileName);
        var relPath = Path.GetRelativePath(_outputDirectory, fullPath);
        ReservePath(relPath);
        var normalized = NormalizeLineEndings(source);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var sha256 = ComputeSha256Hex(bytes);

        File.WriteAllBytes(fullPath, bytes);

        _written.Add(new GeneratedFile(relPath, csharpTypeName, sha256, bytes.Length));
        _sources.Add(relPath, normalized);
        return relPath;
    }

    public bool TryGetSource(string relativePath, out string source) =>
        _sources.TryGetValue(relativePath, out source!);

    /// <summary>
    /// Writes the emitter manifest (accounting report) as JSON.
    /// The manifest file is included in WrittenFiles for complete byte-identity verification.
    /// </summary>
    public void WriteManifest(object manifest, string fileName = "emitter-manifest.json")
    {
        var relativePath = ValidateRelativePath(fileName, nameof(fileName));
        var path = ResolveOwnedPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ReservePath(relativePath);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        var bytes = Encoding.UTF8.GetBytes(NormalizeLineEndings(json));
        File.WriteAllBytes(path, bytes);
        var sha256 = ComputeSha256Hex(bytes);
        var relPath = Path.GetRelativePath(_outputDirectory, path);
        _written.Add(new GeneratedFile(relPath, Path.GetFileNameWithoutExtension(fileName), sha256, bytes.Length));
    }

    private string ResolveOwnedPath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_outputDirectory, relativePath));
        var prefix = _outputDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Generated path '{relativePath}' escapes output root '{_outputDirectory}'.");
        }

        return path;
    }

    private void ReservePath(string relativePath)
    {
        if (!_paths.Add(relativePath))
        {
            throw new InvalidOperationException(
                $"Generated output path collision: '{relativePath}'.");
        }
    }

    private static void ValidateFileStem(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                $"Generated C# type name '{value}' is not a valid file stem.",
                nameof(value));
        }
    }

    private static string ValidateRelativePath(string value, string parameterName)
    {
        if (Path.IsPathRooted(value))
        {
            throw new ArgumentException(
                $"Generated path '{value}' must be relative.",
                parameterName);
        }

        var segments = value.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                $"Generated path '{value}' contains a traversal segment.",
                parameterName);
        }

        return Path.Combine(segments);
    }

    // LF-normalised for byte-stable output across platforms.
    private static string NormalizeLineEndings(string source)
        => source.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string ComputeSha256Hex(byte[] data)
        => Convert.ToHexStringLower(SHA256.HashData(data));
}

public sealed record GeneratedFile(
    string RelativePath,
    string CSharpTypeName,
    string Sha256,
    int ByteLength);

/// <summary>
/// Proves byte identity across two separate runs by comparing sha256 hashes of generated files.
/// </summary>
public static class OutputVerifier
{
    /// <summary>
    /// Compares two sets of generated files. Returns a <see cref="VerificationResult"/>
    /// describing whether the outputs are byte-identical.
    /// </summary>
    public static VerificationResult Verify(
        IReadOnlyList<GeneratedFile> run1,
        IReadOnlyList<GeneratedFile> run2)
    {
        // Byte identity requires exact path casing — use Ordinal, not OrdinalIgnoreCase.
        var r1 = run1.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);
        var r2 = run2.ToDictionary(f => f.RelativePath, StringComparer.Ordinal);

        var mismatches = new List<FileMismatch>();
        var inR1NotR2 = r1.Keys.Except(r2.Keys, StringComparer.Ordinal).ToList();
        var inR2NotR1 = r2.Keys.Except(r1.Keys, StringComparer.Ordinal).ToList();

        foreach (var path in r1.Keys.Intersect(r2.Keys, StringComparer.Ordinal))
        {
            var f1 = r1[path];
            var f2 = r2[path];
            if (!string.Equals(f1.Sha256, f2.Sha256, StringComparison.OrdinalIgnoreCase))
                mismatches.Add(new FileMismatch(path, f1.Sha256, f2.Sha256));
        }

        var identical = mismatches.Count == 0 && inR1NotR2.Count == 0 && inR2NotR1.Count == 0;
        return new VerificationResult(identical, mismatches, inR1NotR2, inR2NotR1);
    }

    /// <summary>
    /// Scans an output directory and builds the list of generated files for verification.
    /// </summary>
    public static IReadOnlyList<GeneratedFile> ScanDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        var result = new List<GeneratedFile>(files.Length);
        foreach (var f in files.OrderBy(x => x, StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(f);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var rel = Path.GetRelativePath(directory, f);
            var csName = Path.GetFileNameWithoutExtension(f).Replace(".g", "");
            result.Add(new GeneratedFile(rel, csName, sha256, bytes.Length));
        }
        return result;
    }
}

public sealed record VerificationResult(
    bool Identical,
    IReadOnlyList<FileMismatch> Mismatches,
    IReadOnlyList<string> OnlyInRun1,
    IReadOnlyList<string> OnlyInRun2);

public sealed record FileMismatch(
    string RelativePath,
    string Run1Sha256,
    string Run2Sha256);
