// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class CallExpression : Node, IMemberExpression, IDeclaration
{
    internal CallExpression() => ((INode)this).Kind = CommentKind.CallExpression;

    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}