// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeAliasDeclaration : DeclarationStatement
{
    public TypeAliasDeclaration()
    {
        Kind = TypeScriptSyntaxKind.TypeAliasDeclaration;
    }

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public ITypeNode Type { get; set; }
}