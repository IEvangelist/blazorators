// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ClassExpression : Node, IClassLikeDeclaration, IPrimaryExpression
{
    internal ClassExpression() => ((INode)this).Kind = CommentKind.ClassExpression;

    NodeArray<TypeParameterDeclaration> IClassLikeDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<HeritageClause> IClassLikeDeclaration.HeritageClauses { get; set; } = default!;
    NodeArray<IClassElement> IClassLikeDeclaration.Members { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
}