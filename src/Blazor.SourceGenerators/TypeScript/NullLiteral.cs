// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class NullLiteral : Node, IPrimaryExpression, ITypeNode
{
    internal NullLiteral() => ((INode)this).Kind = CommentKind.NullKeyword;

    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
    object ITypeNode.TypeNodeBrand { get; set; } = default!;
}