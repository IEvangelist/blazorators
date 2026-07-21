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
        ValidatePackageProfile(profile, profilePath);
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

    private static void ValidatePackageProfile(
        ProfileDefinition profile,
        string profilePath)
    {
        if (profile.EntryPoints is null)
            return;
        if (profile.EntryPoints.Count == 0)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' in '{profilePath}' must declare " +
                "at least one entry point.");
        }

        var duplicateName = profile.EntryPoints
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' has duplicate entry point name " +
                $"'{duplicateName.Key}'.");
        }

        var duplicatePath = profile.EntryPoints
            .GroupBy(entry => entry.JavaScriptPath, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePath is not null)
        {
            throw new InvalidDataException(
                $"Package profile '{profile.Name}' has duplicate JavaScript path " +
                $"'{duplicatePath.Key}'.");
        }

        foreach (var entry in profile.EntryPoints)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrWhiteSpace(entry.Symbol)
                || string.IsNullOrWhiteSpace(entry.JavaScriptPath))
            {
                throw new InvalidDataException(
                    $"Package profile '{profile.Name}' has an incomplete entry point.");
            }
            if (!profile.RootSymbols.Contains(entry.Symbol, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Entry point '{entry.Name}' references '{entry.Symbol}', which is " +
                    "not an exact root symbol.");
            }
            if (!IsValidJavaScriptPath(entry.JavaScriptPath))
            {
                throw new InvalidDataException(
                    $"Entry point '{entry.Name}' has invalid JavaScript path " +
                    $"'{entry.JavaScriptPath}'.");
            }
        }
    }

    private static bool IsValidJavaScriptPath(string path)
        => path.Split('.').All(segment =>
            segment.Length > 0
            && (char.IsLetter(segment[0]) || segment[0] is '_' or '$')
            && segment.Skip(1).All(character =>
                char.IsLetterOrDigit(character) || character is '_' or '$'));
}
