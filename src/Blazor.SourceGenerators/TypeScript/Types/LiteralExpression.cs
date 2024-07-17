// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class LiteralExpression : Node, ILiteralExpression
{
    public object LiteralExpressionBrand { get; set; }
    public string Text { get; set; }
    public bool IsUnterminated { get; set; }
    public bool HasExtendedUnicodeEscape { get; set; }
    public bool IsOctalLiteral { get; set; }
    public object PrimaryExpressionBrand { get; set; }
    public object MemberExpressionBrand { get; set; }
    public object LeftHandSideExpressionBrand { get; set; }
    public object IncrementExpressionBrand { get; set; }
    public object UnaryExpressionBrand { get; set; }

    public object ExpressionBrand { get; set; }
}