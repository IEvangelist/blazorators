using System.Text;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class ProfileOutputPath
{
    private static readonly HashSet<string> ReservedDeviceNames =
        new(
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "CONIN$",
            "CONOUT$",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "COM¹",
            "COM²",
            "COM³",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9",
            "LPT¹",
            "LPT²",
            "LPT³",
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static void ValidateSubdirectory(
        string? outputSubdirectory,
        string profileSource = "profile")
    {
        if (string.IsNullOrWhiteSpace(outputSubdirectory))
            throw Invalid(profileSource, outputSubdirectory, "the path is empty");

        if (Path.IsPathRooted(outputSubdirectory)
            || Path.IsPathFullyQualified(outputSubdirectory))
        {
            throw Invalid(profileSource, outputSubdirectory, "rooted paths are not allowed");
        }

        if (outputSubdirectory.Contains('\\', StringComparison.Ordinal))
        {
            throw Invalid(
                profileSource,
                outputSubdirectory,
                "backslash separators are not allowed; use the portable '/' separator");
        }

        if (outputSubdirectory.Length >= 2
            && char.IsAsciiLetter(outputSubdirectory[0])
            && outputSubdirectory[1] == ':')
        {
            throw Invalid(profileSource, outputSubdirectory, "drive-relative paths are not allowed");
        }

        var segments = outputSubdirectory.Split('/', StringSplitOptions.None);
        if (segments.Length < 2
            || !string.Equals(segments[0], "Profiles", StringComparison.Ordinal))
        {
            throw Invalid(
                profileSource,
                outputSubdirectory,
                "the path must be a strict descendant of 'Profiles'");
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0)
                throw Invalid(profileSource, outputSubdirectory, "empty path segments are not allowed");
            if (segment is "." or "..")
                throw Invalid(profileSource, outputSubdirectory, "'.' and '..' segments are not allowed");
            if (segment[^1] is '.' or ' ')
            {
                throw Invalid(
                    profileSource,
                    outputSubdirectory,
                    "path segments ending in a dot or space are not portable");
            }
            if (segment.Contains(':', StringComparison.Ordinal))
                throw Invalid(profileSource, outputSubdirectory, "':' is not allowed in path segments");
            if (segment.Any(character =>
                    character < ' '
                    || character is '<' or '>' or '"' or '|' or '?' or '*'))
            {
                throw Invalid(
                    profileSource,
                    outputSubdirectory,
                    "Windows-invalid characters are not allowed in path segments");
            }
            if (IsReservedDeviceName(segment))
            {
                throw Invalid(
                    profileSource,
                    outputSubdirectory,
                    $"'{segment}' is a reserved Windows device name");
            }
        }
    }

    internal static void ValidateDistinctSubdirectories(
        IEnumerable<(string OutputSubdirectory, string ProfileSource)> profiles)
    {
        var destinations = new Dictionary<
            string,
            (string OutputSubdirectory, string ProfileSource)>(
            StringComparer.Ordinal);

        foreach (var profile in profiles)
        {
            ValidateSubdirectory(
                profile.OutputSubdirectory,
                profile.ProfileSource);
            var key = PortableDestinationKey(profile.OutputSubdirectory);
            if (destinations.TryGetValue(key, out var existing))
            {
                throw new InvalidDataException(
                    $"Profile outputSubdirectory '{profile.OutputSubdirectory}' in " +
                    $"'{profile.ProfileSource}' aliases '{existing.OutputSubdirectory}' " +
                    $"in '{existing.ProfileSource}' after portable path normalization.");
            }

            destinations.Add(key, profile);
        }
    }

    public static string Resolve(string baseOutputDirectory, string outputSubdirectory)
    {
        ValidateSubdirectory(outputSubdirectory);

        var outputRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(baseOutputDirectory));
        var profilesRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine(outputRoot, "Profiles")));
        var segments = outputSubdirectory.Split('/');
        var destination = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine([outputRoot, .. segments])));
        var descendantPrefix = profilesRoot + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(descendantPrefix, PathComparison))
        {
            throw Invalid(
                "profile",
                outputSubdirectory,
                $"the resolved destination '{destination}' is outside '{profilesRoot}'");
        }

        RejectExistingReparsePoints(profilesRoot, segments.Skip(1));
        RejectExistingPortableAliases(
            outputRoot,
            segments,
            outputSubdirectory);
        return destination;
    }

    private static void RejectExistingReparsePoints(
        string profilesRoot,
        IEnumerable<string> descendantSegments)
    {
        var current = profilesRoot;
        RejectReparsePoint(current);

        foreach (var segment in descendantSegments)
        {
            current = Path.Combine(current, segment);
            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
            return;

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"Profile output path traverses the existing reparse point '{path}'.");
        }
    }

    private static void RejectExistingPortableAliases(
        string outputRoot,
        IReadOnlyList<string> requestedSegments,
        string outputSubdirectory)
    {
        var current = outputRoot;
        foreach (var requestedSegment in requestedSegments)
        {
            if (Directory.Exists(current))
            {
                var matches = Directory
                    .EnumerateFileSystemEntries(current)
                    .Select(Path.GetFileName)
                    .Where(existingSegment =>
                        existingSegment is not null
                        && string.Equals(
                            PortableSegmentKey(existingSegment),
                            PortableSegmentKey(requestedSegment),
                            StringComparison.Ordinal))
                    .Cast<string>()
                    .ToList();

                if (matches.Count > 1
                    || (matches.Count == 1
                        && !string.Equals(
                            matches[0],
                            requestedSegment,
                            StringComparison.Ordinal)))
                {
                    throw Invalid(
                        "profile",
                        outputSubdirectory,
                        $"segment '{requestedSegment}' aliases existing portable path " +
                        $"segment(s) '{string.Join("', '", matches)}'");
                }
            }

            current = Path.Combine(current, requestedSegment);
        }
    }

    private static bool IsReservedDeviceName(string segment)
    {
        var extensionIndex = segment.IndexOf('.');
        var deviceName = extensionIndex < 0
            ? segment
            : segment[..extensionIndex];
        return ReservedDeviceNames.Contains(deviceName);
    }

    private static string PortableDestinationKey(string outputSubdirectory)
        => string.Join(
            '/',
            outputSubdirectory
                .Split('/', StringSplitOptions.None)
                .Select(PortableSegmentKey));

    private static string PortableSegmentKey(string segment)
        => segment
            .TrimEnd(' ', '.')
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();

    private static InvalidDataException Invalid(
        string profileSource,
        string? path,
        string reason)
        => new(
            $"Invalid outputSubdirectory '{path ?? "(null)"}' in '{profileSource}': {reason}.");
}
