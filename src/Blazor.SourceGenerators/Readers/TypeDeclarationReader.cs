// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    private readonly Lazy<string> _typeDeclarationText;
    private IDictionary<string, string>? _typeAliasMap;
    private IDictionary<string, string>? _typeDeclarationMap;
    private ITypeScriptAbstractSyntaxTree? _typeDeclarationTree;

    private TypeDeclarationReader(Uri typeDeclarationUri)
    {
        _typeDeclarationText = new Lazy<string>(valueFactory: () => typeDeclarationUri switch
        {
            { IsAbsoluteUri: true } => GetRemoteResourceText(typeDeclarationUri.AbsoluteUri),
            _ => GetEmbeddedResourceText()
        });
    }

    private TypeDeclarationReader() : this(null!)
    {
    }

    /// <summary>
    /// For testing purposes.
    /// </summary>
    internal bool IsInitialized => TypeDeclarationMap is { Count: > 0 };

    internal string RawSourceText => _typeDeclarationText.Value;

    private IDictionary<string, string> TypeAliasMap =>
        _typeAliasMap ??= ReadTypeAliasMap(_typeDeclarationText.Value);

    private IDictionary<string, string> TypeDeclarationMap =>
        _typeDeclarationMap ??= ReadTypeDeclarationMap(_typeDeclarationText.Value);

    private ITypeScriptAbstractSyntaxTree TypeDeclarationTree =>
        _typeDeclarationTree ??= TypeScriptAbstractSyntaxTree.FromSourceText(_typeDeclarationText.Value);

    public bool TryGetDeclaration(
        string typeName, out string? declaration) =>
        TypeDeclarationMap.TryGetValue(typeName, out declaration);

    public bool TryGetDeclaration(string typeName, out InterfaceDeclaration? declaration)
    {
       declaration = TypeDeclarationTree.RootNode.OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
            .FirstOrDefault(node => node.Identifier == typeName) as InterfaceDeclaration;

        return declaration != null;
    }

    public bool TryGetTypeAlias(
        string typeAliasName, out string? typeAlias) =>
        TypeAliasMap.TryGetValue(typeAliasName, out typeAlias);

    private static ConcurrentDictionary<string, string> ReadTypeAliasMap(string typeDeclarations)
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
            Console.WriteLine($"Error initializing lib dom parser. {ex}");
        }

        return map;
    }

    private static ConcurrentDictionary<string, string> ReadTypeDeclarationMap(string typeDeclarations)
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
            Console.WriteLine($"Error initializing lib dom parser. {ex}");
        }

        return map;
    }
}