// Loads ProfileDefinition instances from *.profile.json files in a directory.

using System.Text.Json;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads a single profile from a .profile.json file.
    /// </summary>
    public static ProfileDefinition Load(string profilePath)
    {
        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<ProfileDefinition>(json, Options)
            ?? throw new InvalidOperationException(
                $"Profile file '{profilePath}' deserialised to null.");
        ProfileOutputPath.ValidateSubdirectory(
            profile.OutputSubdirectory,
            profilePath);
        return profile;
    }

    /// <summary>
    /// Loads all *.profile.json files from a directory.
    /// </summary>
    public static IReadOnlyList<ProfileDefinition> LoadAll(string profileDirectory)
    {
        if (!Directory.Exists(profileDirectory))
            return [];

        var loaded = Directory
            .GetFiles(profileDirectory, "*.profile.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(path => (Path: path, Profile: Load(path)))
            .ToList();
        ProfileOutputPath.ValidateDistinctSubdirectories(loaded.Select(item =>
            (item.Profile.OutputSubdirectory, item.Path)));
        return loaded.Select(item => item.Profile).ToList();
    }
}
