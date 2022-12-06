// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PropertyAccessExpression : Node, IMemberExpression, IDeclaration, IJsxTagNameExpression
{
    public PropertyAccessExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.PropertyAccessExpression;

    public IExpression Expression { get; set; } //LeftHandSideExpression
    public object DeclarationBrand { get; set; }
    public INode? Name { get; set; }
    public object MemberExpressionBrand { get; set; }
    public object LeftHandSideExpressionBrand { get; set; }
    public object IncrementExpressionBrand { get; set; }
    public object UnaryExpressionBrand { get; set; }
    public object ExpressionBrand { get; set; }
}