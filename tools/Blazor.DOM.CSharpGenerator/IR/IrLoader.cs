// IR reader: loads and validates the checked-in JSONL/JSON artifacts against
// the manifest sha256 hashes. Fails hard on any hash mismatch or record-count mismatch.

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.IR;

public sealed class IrLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads and validates the IR. Throws <see cref="IrValidationException"/> on any failure.
    /// </summary>
    public static IrBundle Load(string dataDirectory)
    {
        var manifestPath = Path.Combine(dataDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new IrValidationException($"manifest.json not found in '{dataDirectory}'.");

        var manifest = JsonSerializer.Deserialize<ManifestModel>(
            File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new IrValidationException("manifest.json deserialized to null.");

        if (manifest.SchemaVersion != 1)
            throw new IrValidationException(
                $"Unsupported manifest schemaVersion {manifest.SchemaVersion}. Expected 1.");

        var tsSymbols = LoadJsonlAndValidate<SymbolModel>(
            dataDirectory,
            manifest.Files.TypescriptSymbols,
            "typescript-symbols");

        var webIdlSymbols = LoadJsonlAndValidate<WebIdlSymbolModel>(
            dataDirectory,
            manifest.Files.WebIdlSymbols,
            "webidl-symbols");

        ValidateCoverageHash(dataDirectory, manifest.Files.Coverage);

        return new IrBundle(manifest, tsSymbols, webIdlSymbols);
    }

    private static IReadOnlyList<T> LoadJsonlAndValidate<T>(
        string directory,
        ManifestFileEntryModel entry,
        string label)
    {
        var path = Path.Combine(directory, entry.Path);
        if (!File.Exists(path))
            throw new IrValidationException($"JSONL file '{entry.Path}' not found (expected at '{path}').");

        var rawBytes = File.ReadAllBytes(path);
        var actualHash = ComputeSha256Hex(rawBytes);

        if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IrValidationException(
                $"SHA-256 mismatch for '{entry.Path}'.\n" +
                $"  Expected: {entry.Sha256}\n" +
                $"  Actual  : {actualHash}\n" +
                "The checked-in IR data has been modified without regenerating the manifest.");

        var lines = Encoding.UTF8.GetString(rawBytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length != entry.Records)
            throw new IrValidationException(
                $"Record count mismatch for '{entry.Path}'. " +
                $"Manifest says {entry.Records} but found {lines.Length} lines.");

        var results = new List<T>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            T item;
            try
            {
                item = JsonSerializer.Deserialize<T>(line, JsonOptions)
                    ?? throw new IrValidationException(
                        $"{label}[{i}]: deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new IrValidationException(
                    $"{label}[{i}]: JSON parse error: {ex.Message}");
            }
            results.Add(item);
        }

        return results.AsReadOnly();
    }

    private static void ValidateCoverageHash(string directory, ManifestFileEntryModel entry)
    {
        var path = Path.Combine(directory, entry.Path);
        if (!File.Exists(path))
            throw new IrValidationException($"Coverage file '{entry.Path}' not found.");

        var rawBytes = File.ReadAllBytes(path);
        var actualHash = ComputeSha256Hex(rawBytes);

        if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IrValidationException(
                $"SHA-256 mismatch for coverage file '{entry.Path}'.\n" +
                $"  Expected: {entry.Sha256}\n" +
                $"  Actual  : {actualHash}");
    }

    public static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }
}

public sealed record IrBundle(
    ManifestModel Manifest,
    IReadOnlyList<SymbolModel> TypescriptSymbols,
    IReadOnlyList<WebIdlSymbolModel> WebIdlSymbols);

public sealed class IrValidationException(string message) : Exception(message);
