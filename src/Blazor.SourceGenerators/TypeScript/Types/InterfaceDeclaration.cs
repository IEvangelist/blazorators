// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class InterfaceDeclaration : DeclarationStatement
{
    public InterfaceDeclaration()
    {
        Kind = TypeScriptSyntaxKind.InterfaceDeclaration;
    }

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<HeritageClause> HeritageClauses { get; set; }
    public NodeArray<ITypeElement> Members { get; set; }
}