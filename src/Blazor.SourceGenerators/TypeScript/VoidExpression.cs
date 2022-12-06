// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class VoidExpression : UnaryExpression
{
    public VoidExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.VoidExpression;

    public IExpression Expression { get; set; }
}