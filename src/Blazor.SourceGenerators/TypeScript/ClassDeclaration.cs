// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ClassDeclaration : Node, IClassLikeDeclaration, IDeclarationStatement
{
    internal ClassDeclaration() => ((INode)this).Kind = CommentKind.ClassDeclaration;

    INode IDeclaration.Name { get; set; } = default!;
    NodeArray<TypeParameterDeclaration> IClassLikeDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<HeritageClause> IClassLikeDeclaration.HeritageClauses { get; set; } = default!;
    NodeArray<IClassElement> IClassLikeDeclaration.Members { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    object IStatement.StatementBrand { get; set; } = default!;
}