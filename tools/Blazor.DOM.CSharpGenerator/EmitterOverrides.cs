// Emitter overrides: loads and validates the checked-in emitter-overrides.json.
// An override must have a non-empty rationale; overrides without rationale are rejected.
// An empty overrides file is valid — it means all ambiguous symbols will fail generation.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blazor.DOM.CSharpGenerator;

/// <summary>
/// A single explicit classification override for an ambiguous symbol.
/// </summary>
public sealed record EmitterOverrideEntry(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("rationale")] string Rationale
);

/// <summary>
/// Root model for emitter-overrides.json.
/// </summary>
public sealed record EmitterOverridesFile(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("ambiguousSymbolOverrides")]
    IReadOnlyList<EmitterOverrideEntry> AmbiguousSymbolOverrides
);

/// <summary>
/// Loads and validates the emitter-overrides.json from the data directory.
/// </summary>
public static class EmitterOverridesLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads the override file. Returns an empty dictionary if the file is absent.
    /// Throws <see cref="EmitterOverridesException"/> on any schema/validation error.
    /// </summary>
    public static IReadOnlyDictionary<string, EmitterOverrideEntry> Load(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "emitter-overrides.json");
        if (!File.Exists(path))
        {
            // Absent file is allowed: means no overrides (all ambiguous symbols fail)
            return new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);
        }

        EmitterOverridesFile file;
        try
        {
            file = JsonSerializer.Deserialize<EmitterOverridesFile>(
                File.ReadAllText(path), JsonOptions)
                ?? throw new EmitterOverridesException($"'{path}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new EmitterOverridesException($"JSON parse error in '{path}': {ex.Message}", ex);
        }

        if (file.SchemaVersion != 1)
            throw new EmitterOverridesException(
                $"Unsupported emitter-overrides schemaVersion {file.SchemaVersion}. Expected 1.");

        var result = new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);
        foreach (var entry in file.AmbiguousSymbolOverrides)
        {
            if (string.IsNullOrWhiteSpace(entry.Symbol))
                throw new EmitterOverridesException(
                    "emitter-overrides.json contains an entry with an empty 'symbol' field.");

            if (string.IsNullOrWhiteSpace(entry.Classification))
                throw new EmitterOverridesException(
                    $"emitter-overrides.json: entry for '{entry.Symbol}' has an empty 'classification'.");

            if (string.IsNullOrWhiteSpace(entry.Rationale) || entry.Rationale.Trim().Length < 10)
                throw new EmitterOverridesException(
                    $"emitter-overrides.json: entry for '{entry.Symbol}' has an insufficient rationale " +
                    "(must be at least 10 non-whitespace characters). Overrides require a reviewed rationale.");

            if (result.ContainsKey(entry.Symbol))
                throw new EmitterOverridesException(
                    $"emitter-overrides.json: duplicate override for '{entry.Symbol}'.");

            result[entry.Symbol] = entry;
        }

        return result;
    }
}

public sealed class EmitterOverridesException(string message, Exception? inner = null)
    : Exception(message, inner);
