// IR reader: loads and validates the checked-in JSONL/JSON artifacts against
// the manifest sha256 hashes. Fails hard on any hash mismatch or record-count mismatch.

using System.Security.Cryptography;
using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.IR;

public sealed class IrLoader
{
    private const int MinimumSupportedSchemaVersion = 1;
    private const int MaximumSupportedSchemaVersion = 2;

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
        => Load(dataDirectory, retainWebIdlSymbols: true);

    /// <summary>
    /// Loads the TypeScript IR used by the emitter while validating the locked
    /// Web IDL hash, record count, and JSON shape without retaining its records.
    /// </summary>
    public static IrBundle LoadForGeneration(string dataDirectory)
        => Load(dataDirectory, retainWebIdlSymbols: false);

    private static IrBundle Load(
        string dataDirectory,
        bool retainWebIdlSymbols)
    {
        var manifestPath = Path.Combine(dataDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new IrValidationException($"manifest.json not found in '{dataDirectory}'.");

        var manifest = JsonSerializer.Deserialize<ManifestModel>(
            File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new IrValidationException("manifest.json deserialized to null.");

        if (manifest.SchemaVersion is < MinimumSupportedSchemaVersion or > MaximumSupportedSchemaVersion)
            throw new IrValidationException(
                $"Unsupported manifest schemaVersion {manifest.SchemaVersion}. " +
                $"Supported versions are {MinimumSupportedSchemaVersion} through {MaximumSupportedSchemaVersion}.");

        var tsSymbols = LoadJsonlAndValidate<SymbolModel>(
            dataDirectory,
            manifest.Files.TypescriptSymbols,
            "typescript-symbols");

        IReadOnlyList<WebIdlSymbolModel> webIdlSymbols;
        if (retainWebIdlSymbols)
        {
            webIdlSymbols = LoadJsonlAndValidate<WebIdlSymbolModel>(
                dataDirectory,
                manifest.Files.WebIdlSymbols,
                "webidl-symbols");
        }
        else
        {
            ValidateJsonlWithoutRetaining(
                dataDirectory,
                manifest.Files.WebIdlSymbols,
                "webidl-symbols");
            webIdlSymbols = [];
        }

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

        ValidateFileHash(path, entry);

        var results = new List<T>(entry.Records);
        var recordIndex = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            results.Add(DeserializeLine<T>(line, label, recordIndex));
            recordIndex++;
        }

        ValidateRecordCount(entry, recordIndex);
        return results.AsReadOnly();
    }

    private static void ValidateJsonlWithoutRetaining(
        string directory,
        ManifestFileEntryModel entry,
        string label)
    {
        var path = Path.Combine(directory, entry.Path);
        if (!File.Exists(path))
            throw new IrValidationException(
                $"JSONL file '{entry.Path}' not found (expected at '{path}').");

        ValidateFileHash(path, entry);

        var recordIndex = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new IrValidationException(
                        $"{label}[{recordIndex}]: expected a JSON object.");
                }
            }
            catch (JsonException ex)
            {
                throw new IrValidationException(
                    $"{label}[{recordIndex}]: JSON parse error: {ex.Message}");
            }
            recordIndex++;
        }

        ValidateRecordCount(entry, recordIndex);
    }

    private static T DeserializeLine<T>(
        string line,
        string label,
        int recordIndex)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(line, JsonOptions)
                ?? throw new IrValidationException(
                    $"{label}[{recordIndex}]: deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new IrValidationException(
                $"{label}[{recordIndex}]: JSON parse error: {ex.Message}");
        }
    }

    private static void ValidateFileHash(
        string path,
        ManifestFileEntryModel entry)
    {
        using var stream = File.OpenRead(path);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (!string.Equals(
                actualHash,
                entry.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IrValidationException(
                $"SHA-256 mismatch for '{entry.Path}'.\n" +
                $"  Expected: {entry.Sha256}\n" +
                $"  Actual  : {actualHash}\n" +
                "The checked-in IR data has been modified without regenerating the manifest.");
        }
    }

    private static void ValidateRecordCount(
        ManifestFileEntryModel entry,
        int actualRecords)
    {
        if (actualRecords != entry.Records)
        {
            throw new IrValidationException(
                $"Record count mismatch for '{entry.Path}'. " +
                $"Manifest says {entry.Records} but found {actualRecords} lines.");
        }
    }

    private static void ValidateCoverageHash(string directory, ManifestFileEntryModel entry)
    {
        var path = Path.Combine(directory, entry.Path);
        if (!File.Exists(path))
            throw new IrValidationException($"Coverage file '{entry.Path}' not found.");

        using var stream = File.OpenRead(path);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));

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
