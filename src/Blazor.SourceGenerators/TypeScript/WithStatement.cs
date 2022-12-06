// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class WithStatement : Statement
{
    public WithStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.WithStatement;

    public IExpression Expression { get; set; }
    public IStatement Statement { get; set; }
}