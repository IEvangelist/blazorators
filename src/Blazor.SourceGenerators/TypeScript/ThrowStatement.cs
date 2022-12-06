// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ThrowStatement : Statement
{
    public ThrowStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ThrowStatement;

    public IExpression Expression { get; set; }
}