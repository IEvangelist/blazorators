// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ParenthesizedExpression : PrimaryExpression
{
    public ParenthesizedExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.ParenthesizedExpression;

    public IExpression Expression { get; set; }
}