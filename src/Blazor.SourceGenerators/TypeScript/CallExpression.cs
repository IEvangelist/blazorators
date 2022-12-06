// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class CallExpression : Node, IMemberExpression, IDeclaration
{
    public CallExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.CallExpression;

    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}