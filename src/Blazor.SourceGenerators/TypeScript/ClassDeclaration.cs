// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ClassDeclaration : Node, IClassLikeDeclaration, IDeclarationStatement
{
    public ClassDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ClassDeclaration;

    INode? IDeclaration.Name? { get; set; }
    NodeArray<TypeParameterDeclaration>? IClassLikeDeclaration.TypeParameters? { get; set; }
    NodeArray<HeritageClause>? IClassLikeDeclaration.HeritageClauses? { get; set; }
    NodeArray<IClassElement>? IClassLikeDeclaration.Members? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    object IStatement.StatementBrand? { get; set; }
}