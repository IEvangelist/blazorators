// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ReturnStatement : Statement
{
    public ReturnStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ReturnStatement;

    public IExpression Expression { get; set; }
}