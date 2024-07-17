// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Readers;

internal sealed partial class TypeDeclarationReader
{
    private readonly Lazy<string> _typeDeclarationText;
    private ITypeScriptAbstractSyntaxTree? _typescriptAbstractSyntaxTree;

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
    internal bool IsInitialized => TypescriptAbstractSyntaxTree.RootNode is { Count: > 0 };

    internal string RawSourceText => _typeDeclarationText.Value;

    private ITypeScriptAbstractSyntaxTree TypescriptAbstractSyntaxTree =>
        _typescriptAbstractSyntaxTree ??= TypeScriptAbstractSyntaxTree.FromSourceText(_typeDeclarationText.Value);

    public bool TryGetInterface(string typeName, out InterfaceDeclaration? declaration)
    {
        declaration = TypescriptAbstractSyntaxTree.RootNode.OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
            .FirstOrDefault(node => node.Identifier == typeName) as InterfaceDeclaration;

        return declaration != null;
    }

    public bool TryGetTypeAlias(string typeName, out TypeAliasDeclaration? declaration)
    {
        declaration = TypescriptAbstractSyntaxTree.RootNode.OfKind(TypeScriptSyntaxKind.TypeAliasDeclaration)
             .FirstOrDefault(node => node.Identifier == typeName) as TypeAliasDeclaration;

        return declaration != null;
    }
}