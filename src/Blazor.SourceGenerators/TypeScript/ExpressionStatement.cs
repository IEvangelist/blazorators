// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ExpressionStatement : Statement
{
    public ExpressionStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ExpressionStatement;

    public IExpression? Expression { get; set; }
}