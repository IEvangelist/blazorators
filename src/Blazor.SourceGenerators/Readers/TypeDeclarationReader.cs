// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    private readonly Lazy<string> _typeDeclarationText;
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
    internal bool IsInitialized => TypeDeclarationTree.RootNode is { Count: > 0 };

    internal string RawSourceText => _typeDeclarationText.Value;

    private ITypeScriptAbstractSyntaxTree TypeDeclarationTree =>
        _typeDeclarationTree ??= TypeScriptAbstractSyntaxTree.FromSourceText(_typeDeclarationText.Value);

    public bool TryGetDeclaration(string typeName, out string? declaration)
    {
        var node = TypeDeclarationTree.RootNode.OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
             .FirstOrDefault(node => node.Identifier == typeName) as InterfaceDeclaration;

        declaration = node == null
            ? string.Empty
            : node.GetText().ToString();

        declaration = declaration.TrimStart('\r', '\n');

        return declaration != null;
    }

    public bool TryGetTypeAlias(string typeName, out string? declaration)
    {
        var node = TypeDeclarationTree.RootNode.OfKind(TypeScriptSyntaxKind.TypeAliasDeclaration)
             .FirstOrDefault(node => node.Identifier == typeName) as TypeAliasDeclaration;

        declaration = node == null
            ? string.Empty
            : node.GetText().ToString();

        declaration = declaration.TrimStart('\r', '\n');

        return declaration != null;
    }
}