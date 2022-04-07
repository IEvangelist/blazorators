// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    readonly Uri _typeDeclarationSource;
    readonly Lazy<string> _typeDeclarationText;

    IDictionary<string, string>? _typeDeclarationMap;
    IDictionary<string, string>? _typeAliasMap;

    private IDictionary<string, string> TypeDeclarationMap =>
        _typeDeclarationMap ??= ReadTypeDeclarationMap(_typeDeclarationText.Value);

    private IDictionary<string, string> TypeAliasMap =>
        _typeAliasMap ??= ReadTypeAliasMap(_typeDeclarationText.Value);

    private TypeDeclarationReader(
        Uri? typeDeclarationSource = null)
    {
        _typeDeclarationSource = typeDeclarationSource ?? s_defaultTypeDeclarationSource;
        _typeDeclarationText = new Lazy<string>(
            valueFactory: () => _typeDeclarationSource.IsFile
                ? GetLocalFileText(_typeDeclarationSource.LocalPath)
                : GetRemoteFileText(_typeDeclarationSource.OriginalString));
    }

    IDictionary<string, string> ReadTypeDeclarationMap(string typeDeclarations)
    {
        ConcurrentDictionary<string, string> map = new();
            
        try
        {
            if (typeDeclarations is { Length: > 0 })
            {
                var matchCollection =
                    InterfaceRegex.Matches(typeDeclarations).Cast<Match>().Select(m => m.Value);
                Parallel.ForEach(
                    matchCollection,
                    match =>
                    {
                        var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(match, "TypeName");
                        if (typeName is not null)
                        {
                            map[typeName] = match;
                        }
                    });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error intializing lib dom parser. {ex}");
        }

        return map;
    }

    IDictionary<string, string> ReadTypeAliasMap(string typeDeclarations)
    {
        ConcurrentDictionary<string, string> map = new();

        try
        {
            if (typeDeclarations is { Length: > 0 })
            {
                var matchCollection =
                    TypeRegex.Matches(typeDeclarations).Cast<Match>().Select(m => m.Value);
                Parallel.ForEach(
                    matchCollection,
                    match =>
                    {
                        var typeName = TypeNameRegex.GetMatchGroupValue(match, "TypeName");
                        if (typeName is not null)
                        {
                            map[typeName] = match;
                        }
                    });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error intializing lib dom parser. {ex}");
        }

        return map;
    }

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => TypeDeclarationMap is { Count: > 0 };

    public bool TryGetDeclaration(
        string typeName, out string? declaration) =>
        TypeDeclarationMap.TryGetValue(typeName, out declaration);

    public bool TryGetTypeAlias(
        string typeAliasName, out string? typeAlias) =>
        TypeAliasMap.TryGetValue(typeAliasName, out typeAlias);
}
