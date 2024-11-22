// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    readonly Lazy<string> _typeDeclarationText;

    private IDictionary<string, string> TypeDeclarationMap =>
        field ??= ReadTypeDeclarationMap(_typeDeclarationText.Value);

    private IDictionary<string, string> TypeAliasMap =>
        field ??= ReadTypeAliasMap(_typeDeclarationText.Value);

#pragma warning disable CS9264 // Non-nullable property must contain a non-null value when exiting constructor. Consider adding the 'required' modifier, or declaring the property as nullable, or adding '[field: MaybeNull, AllowNull]' attributes.
    private TypeDeclarationReader()
#pragma warning restore CS9264 // Non-nullable property must contain a non-null value when exiting constructor. Consider adding the 'required' modifier, or declaring the property as nullable, or adding '[field: MaybeNull, AllowNull]' attributes.
    {
        _typeDeclarationText = new Lazy<string>(
            valueFactory: () => GetEmbeddedResourceText());
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
            Trace.WriteLine($"Error initializing lib dom parser. {ex}");
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
            Trace.WriteLine($"Error initializing lib dom parser. {ex}");
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
