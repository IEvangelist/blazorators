// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class BooleanLiteral : Node, IPrimaryExpression, ITypeNode
{
    public BooleanLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.BooleanKeyword;

    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
    object ITypeNode.TypeNodeBrand? { get; set; }
}