// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ClassDeclaration : Node, IClassLikeDeclaration, IDeclarationStatement
{
    public ClassDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ClassDeclaration;
    }

    public INode Name { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<HeritageClause> HeritageClauses { get; set; }
    public NodeArray<IClassElement> Members { get; set; }
    public object DeclarationBrand { get; set; }
    public object StatementBrand { get; set; }
}