// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class WhileStatement : IterationStatement
{
    public WhileStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.WhileStatement;

    public IExpression Expression { get; set; }
}