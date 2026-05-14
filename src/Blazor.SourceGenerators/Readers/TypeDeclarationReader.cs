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
