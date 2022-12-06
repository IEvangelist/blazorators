// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class LiteralExpression : Node, ILiteralExpression, IPrimaryExpression
{
    object ILiteralExpression.LiteralExpressionBrand? { get; set; }
    string ILiteralLikeNode.Text? { get; set; }
    bool ILiteralLikeNode.IsUnterminated { get; set; }
    bool ILiteralLikeNode.HasExtendedUnicodeEscape { get; set; }
    bool ILiteralLikeNode.IsOctalLiteral { get; set; }
    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
}