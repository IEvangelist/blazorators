// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ClassExpression : Node, IClassLikeDeclaration, IPrimaryExpression
{
    public ClassExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.ClassExpression;

    NodeArray<TypeParameterDeclaration>? IClassLikeDeclaration.TypeParameters? { get; set; }
    NodeArray<HeritageClause>? IClassLikeDeclaration.HeritageClauses? { get; set; }
    NodeArray<IClassElement>? IClassLikeDeclaration.Members? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
}