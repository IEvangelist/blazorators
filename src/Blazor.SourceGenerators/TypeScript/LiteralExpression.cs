// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class LiteralExpression : Node, ILiteralExpression, IPrimaryExpression
{
    object ILiteralExpression.LiteralExpressionBrand { get; set; } = default!;
    string ILiteralLikeNode.Text { get; set; } = default!;
    bool ILiteralLikeNode.IsUnterminated { get; set; }
    bool ILiteralLikeNode.HasExtendedUnicodeEscape { get; set; }
    bool ILiteralLikeNode.IsOctalLiteral { get; set; }
    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
}