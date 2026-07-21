using System.Text.RegularExpressions;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Profiles;

namespace Blazor.DOM.CSharpGenerator.Anchors;

public sealed record InteropAnchor(
    string InterfaceName,
    string TypeName,
    string JavaScriptPath,
    string Scope,
    string SourcePath);

public static partial class InteropAnchorLoader
{
    public static IReadOnlyList<InteropAnchor> Load(string anchorsDirectory)
    {
        if (!Directory.Exists(anchorsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"DOM anchor directory does not exist: '{anchorsDirectory}'.");
        }

        var anchors = Directory
            .EnumerateFiles(anchorsDirectory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Parse(anchorsDirectory, path))
            .ToList();

        var duplicate = anchors
            .GroupBy(anchor => (anchor.Scope, anchor.TypeName))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Duplicate DOM anchor for '{duplicate.Key.TypeName}' in " +
                $"scope '{duplicate.Key.Scope}'.");
        }

        return anchors;
    }

    public static HostPackageOptions CreateExhaustiveOptions(
        IReadOnlyList<InteropAnchor> anchors)
    {
        var entryPoints = anchors
            .Where(anchor => string.Equals(
                anchor.Scope,
                "Exhaustive",
                StringComparison.Ordinal))
            .Select(ToEntryPoint)
            .OrderBy(entryPoint => entryPoint.Name, StringComparer.Ordinal)
            .ToList();
        if (entryPoints.Count == 0)
            throw new InvalidDataException("No exhaustive DOM anchors were found.");

        return new HostPackageOptions(
            new HostCapabilityMetadata(
                "DOM",
                "Exhaustive browser DOM bindings.",
                [],
                false,
                false,
                entryPoints),
            EmitCapabilityFacade: false);
    }

    public static ProfileDefinition Apply(
        ProfileDefinition profile,
        IReadOnlyList<InteropAnchor> anchors)
    {
        if (profile.EntryPoints is null)
            return profile;

        var scope = $"Profiles/{profile.Name}";
        var profileAnchors = anchors
            .Where(anchor => string.Equals(
                anchor.Scope,
                scope,
                StringComparison.Ordinal))
            .Select(ToEntryPoint)
            .OrderBy(entryPoint => entryPoint.Name, StringComparer.Ordinal)
            .ToList();
        var configured = profile.EntryPoints
            .Select(Normalize)
            .OrderBy(entryPoint => entryPoint.Name, StringComparer.Ordinal)
            .ToList();

        if (!profileAnchors.SequenceEqual(configured))
        {
            throw new InvalidDataException(
                $"Profile '{profile.Name}' entry points do not match its versioned " +
                $"[JSAutoInterop] anchors in '{scope}'.");
        }

        return profile;
    }

    private static InteropAnchor Parse(string anchorsDirectory, string path)
    {
        var source = File.ReadAllText(path);
        var declaration = AnchorDeclarationRegex().Match(source);
        if (!declaration.Success)
        {
            throw new InvalidDataException(
                $"DOM anchor '{path}' must declare one public partial interface " +
                "decorated with [JSAutoInterop].");
        }

        var arguments = declaration.Groups["arguments"].Value;
        var typeName = ReadRequiredArgument(arguments, "TypeName", path);
        var implementation = ReadRequiredArgument(arguments, "Implementation", path);
        var relativeDirectory = Path.GetDirectoryName(
            Path.GetRelativePath(anchorsDirectory, path));
        var scope = relativeDirectory?.Replace('\\', '/') ?? string.Empty;

        return new InteropAnchor(
            declaration.Groups["interface"].Value,
            typeName,
            NormalizeJavaScriptPath(implementation),
            scope,
            path);
    }

    private static string ReadRequiredArgument(
        string arguments,
        string name,
        string path)
    {
        var match = Regex.Match(
            arguments,
            $@"\b{Regex.Escape(name)}\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"DOM anchor '{path}' must specify {name}.");
        }

        return match.Groups["value"].Value;
    }

    private static HostEntryPoint ToEntryPoint(InteropAnchor anchor)
        => new(
            anchor.TypeName,
            anchor.TypeName,
            anchor.JavaScriptPath);

    private static HostEntryPoint Normalize(HostEntryPoint entryPoint)
        => entryPoint with
        {
            JavaScriptPath = NormalizeJavaScriptPath(entryPoint.JavaScriptPath),
        };

    private static string NormalizeJavaScriptPath(string path)
        => path.StartsWith("window.", StringComparison.Ordinal)
            ? path["window.".Length..]
            : path;

    [GeneratedRegex(
        @"\[JSAutoInterop\s*\((?<arguments>.*?)\)\]\s*public\s+partial\s+interface\s+(?<interface>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorDeclarationRegex();
}
