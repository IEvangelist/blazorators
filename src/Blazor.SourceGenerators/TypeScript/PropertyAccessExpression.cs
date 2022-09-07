// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PropertyAccessExpression : Node, IMemberExpression, IDeclaration, IJsxTagNameExpression
{
    internal PropertyAccessExpression() => ((INode)this).Kind = CommentKind.PropertyAccessExpression;

    internal IExpression Expression { get; set; } //LeftHandSideExpression
    internal object DeclarationBrand { get; set; }
    internal INode Name { get; set; }
    internal object MemberExpressionBrand { get; set; }
    internal object LeftHandSideExpressionBrand { get; set; }
    internal object IncrementExpressionBrand { get; set; }
    internal object UnaryExpressionBrand { get; set; }
    internal object ExpressionBrand { get; set; }
}