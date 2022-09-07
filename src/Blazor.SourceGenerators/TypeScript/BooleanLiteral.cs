// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class BooleanLiteral : Node, IPrimaryExpression, ITypeNode
{
    internal BooleanLiteral() => ((INode)this).Kind = CommentKind.BooleanKeyword;

    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
    object ITypeNode.TypeNodeBrand { get; set; } = default!;
}