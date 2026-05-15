// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    private readonly IDictionary<string, string> _typeDeclarationMap;
    private readonly IDictionary<string, string> _typeAliasMap;

    private TypeDeclarationReader()
    {
        var text = GetEmbeddedResourceText();
        // Build both maps eagerly from the source text so we can release the
        // raw ~800KB string immediately. Holding it in a Lazy<string> kept it
        // alive for the host process lifetime via the static reader cache.
        _typeDeclarationMap = ReadTypeDeclarationMap(text);
        _typeAliasMap = ReadTypeAliasMap(text);
    }

    /// <summary>
    /// Builds a reader from caller-supplied TypeScript declaration text.
    /// This is the entry point used by the generator's
    /// <c>AdditionalTextsProvider</c> path: the consumer ships a <c>.d.ts</c>
    /// via <c>&lt;AdditionalFiles&gt;</c>, and the generator hands the text
    /// content to this constructor so the parser can resolve interfaces
    /// declared outside <c>lib.dom.d.ts</c>.
    /// </summary>
    internal TypeDeclarationReader(string declarationText)
    {
        // The shared interface/type regexes use `$` in multiline mode,
        // which only matches before `\n` - not before `\r`. The embedded
        // lib.dom.d.ts is shipped with LF endings (see T1.8); a consumer-
        // supplied `.d.ts` may have CRLF if it was checked out on Windows.
        // Normalize here so the regex matches uniformly.
        var normalized = declarationText?.Replace("\r\n", "\n") ?? string.Empty;
        _typeDeclarationMap = ReadTypeDeclarationMap(normalized);
        _typeAliasMap = ReadTypeAliasMap(normalized);
    }

    private static IDictionary<string, string> ReadTypeDeclarationMap(string typeDeclarations)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        try
        {
            if (typeDeclarations is { Length: > 0 })
            {
                foreach (Match match in InterfaceRegex.Matches(typeDeclarations))
                {
                    var matchValue = match.Value;
                    var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(matchValue, "TypeName");
                    if (typeName is not null)
                    {
                        map[typeName] = matchValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error initializing lib dom parser. {ex}");
        }

        return map;
    }

    private static IDictionary<string, string> ReadTypeAliasMap(string typeDeclarations)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        try
        {
            if (typeDeclarations is { Length: > 0 })
            {
                foreach (Match match in TypeRegex.Matches(typeDeclarations))
                {
                    var matchValue = match.Value;
                    var typeName = TypeNameRegex.GetMatchGroupValue(matchValue, "TypeName");
                    if (typeName is not null)
                    {
                        map[typeName] = matchValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error initializing lib dom parser. {ex}");
        }

        return map;
    }

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => _typeDeclarationMap is { Count: > 0 };

    public bool TryGetDeclaration(
        string typeName, out string? declaration) =>
        _typeDeclarationMap.TryGetValue(typeName, out declaration);

    public bool TryGetTypeAlias(
        string typeAliasName, out string? typeAlias) =>
        _typeAliasMap.TryGetValue(typeAliasName, out typeAlias);
}
