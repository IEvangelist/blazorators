// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PartiallyEmittedExpression : LeftHandSideExpression
{
    public PartiallyEmittedExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.PartiallyEmittedExpression;

    public IExpression Expression { get; set; }
}