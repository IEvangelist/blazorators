// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class DoStatement : IterationStatement
{
    public DoStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.DoStatement;

    public IExpression? Expression { get; set; }
}