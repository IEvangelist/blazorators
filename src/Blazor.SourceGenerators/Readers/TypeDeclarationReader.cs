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
                    var rawTypeName = InterfaceTypeNameRegex.GetMatchGroupValue(matchValue, "TypeName");
                    var typeName = NormalizeTypeName(rawTypeName);
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        map[typeName!] = matchValue;
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

    /// <summary>
    /// Enumerates the names of every interface declaration discovered in the
    /// source text. Exposed for the corpus-coverage test harness; callers
    /// should not depend on ordering.
    /// </summary>
    internal IEnumerable<string> DeclarationNames => _typeDeclarationMap.Keys;

    /// <summary>
    /// Enumerates the names of every type alias discovered in the source
    /// text. Exposed for the corpus-coverage test harness; callers should not
    /// depend on ordering.
    /// </summary>
    internal IEnumerable<string> TypeAliasNames => _typeAliasMap.Keys;

    /// <summary>
    /// Total number of interface declarations indexed.
    /// </summary>
    internal int DeclarationCount => _typeDeclarationMap.Count;

    /// <summary>
    /// Total number of type aliases indexed.
    /// </summary>
    internal int TypeAliasCount => _typeAliasMap.Count;

    public bool TryGetDeclaration(
        string typeName, out string? declaration) =>
        _typeDeclarationMap.TryGetValue(typeName, out declaration);

    public bool TryGetTypeAlias(
        string typeAliasName, out string? typeAlias) =>
        _typeAliasMap.TryGetValue(typeAliasName, out typeAlias);

    /// <summary>
    /// When the supplied alias is a string-literal union (e.g.
    /// <c>type DocumentReadyState = "complete" | "interactive" |
    /// "loading";</c>), returns <see langword="true"/> and the distinct
    /// ordered list of raw string values. Returns <see langword="false"/>
    /// for unknown aliases, identifier unions, primitive aliases,
    /// function-type aliases, and any other non-string-literal alias
    /// shape. Thin wrapper around
    /// <see cref="ClassifyStringLiteralUnion"/> preserved for callers
    /// (and tests) that only need a boolean disposition.
    /// </summary>
    public bool TryGetStringLiteralUnion(
        string aliasName, out IReadOnlyList<string> rawMembers) =>
        ClassifyStringLiteralUnion(aliasName, out rawMembers)
            == StringLiteralUnionClassification.StringLiteralUnion;

    /// <summary>
    /// Three-way classification for a TypeScript type alias lookup. Lets
    /// callers distinguish "alias not found at all" (BR0006) from "alias
    /// exists but isn't a string-literal union" (BR0008) without having
    /// to call <see cref="TryGetTypeAlias"/> separately.
    /// </summary>
    internal enum StringLiteralUnionClassification
    {
        AliasNotFound,
        NotStringLiteralUnion,
        StringLiteralUnion,
    }

    /// <summary>
    /// Classifies the supplied alias name as a string-literal union (and
    /// returns its raw members), an alias that exists but is the wrong
    /// shape, or an unknown alias. Use this in preference to a
    /// separate <see cref="TryGetTypeAlias"/> call when the caller needs
    /// to emit a different diagnostic for each failure mode.
    /// </summary>
    public StringLiteralUnionClassification ClassifyStringLiteralUnion(
        string aliasName, out IReadOnlyList<string> rawMembers)
    {
        rawMembers = Array.Empty<string>();
        if (!_typeAliasMap.TryGetValue(aliasName, out var aliasText) ||
            aliasText is null)
        {
            return StringLiteralUnionClassification.AliasNotFound;
        }

        var equalsIndex = aliasText.IndexOf('=');
        if (equalsIndex < 0 || equalsIndex == aliasText.Length - 1)
        {
            return StringLiteralUnionClassification.NotStringLiteralUnion;
        }

        var body = aliasText.Substring(equalsIndex + 1);
        if (StringLiteralUnionDetector.TryParse(body, out rawMembers))
        {
            return StringLiteralUnionClassification.StringLiteralUnion;
        }

        rawMembers = Array.Empty<string>();
        return StringLiteralUnionClassification.NotStringLiteralUnion;
    }
}
