// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ThisExpression : Node, IPrimaryExpression, IKeywordTypeNode
{
    public ThisExpression()
    {
        Kind = TypeScriptSyntaxKind.ThisKeyword;
    }

    public object TypeNodeBrand { get; set; }
    public object PrimaryExpressionBrand { get; set; }
    public object MemberExpressionBrand { get; set; }
    public object LeftHandSideExpressionBrand { get; set; }
    public object IncrementExpressionBrand { get; set; }
    public object UnaryExpressionBrand { get; set; }
    public object ExpressionBrand { get; set; }
}