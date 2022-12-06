// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class YieldExpression : Expression
{
    public YieldExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.YieldExpression;

    public AsteriskToken AsteriskToken { get; set; }
    public IExpression Expression { get; set; }
}