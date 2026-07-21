// Profile definition model — describes a focused DOM capability package.
// Profile JSON files live at data/Blazor.DOM.Profiles/*.profile.json.

using System.Text.Json.Serialization;

namespace Blazor.DOM.CSharpGenerator.Profiles;

/// <summary>
/// A focused DOM capability profile that restricts generation to a named set of root symbols
/// and their transitive dependencies.
/// </summary>
public sealed record ProfileDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("rootSymbols")] IReadOnlyList<string> RootSymbols,
    [property: JsonPropertyName("secureContext")] bool SecureContext,
    [property: JsonPropertyName("requiresUserActivation")] bool RequiresUserActivation,
    [property: JsonPropertyName("features")] IReadOnlyList<string> Features,
    [property: JsonPropertyName("outputNamespace")] string OutputNamespace,
    [property: JsonPropertyName("outputSubdirectory")] string OutputSubdirectory,
    [property: JsonPropertyName("memberIncludes")]
        IReadOnlyDictionary<string, IReadOnlyList<string>>? MemberIncludes = null,
    [property: JsonPropertyName("minimalDependencyContracts")]
        bool MinimalDependencyContracts = false
);
