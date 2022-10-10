// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ParenthesizedExpression : PrimaryExpression
{
    internal ParenthesizedExpression() => ((INode)this).Kind = SyntaxKind.ParenthesizedExpression;

    internal IExpression Expression { get; set; }
}