// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ThisExpression : Node, IPrimaryExpression, IKeywordTypeNode
{
    internal ThisExpression() => ((INode)this).Kind = CommentKind.ThisKeyword;

    internal object TypeNodeBrand { get; set; }
    internal object PrimaryExpressionBrand { get; set; }
    internal object MemberExpressionBrand { get; set; }
    internal object LeftHandSideExpressionBrand { get; set; }
    internal object IncrementExpressionBrand { get; set; }
    internal object UnaryExpressionBrand { get; set; }
    internal object ExpressionBrand { get; set; }
}