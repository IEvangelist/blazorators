// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class NullLiteral : Node, IPrimaryExpression, ITypeNode
{
    public NullLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.NullKeyword;

    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
    object ITypeNode.TypeNodeBrand? { get; set; }
}