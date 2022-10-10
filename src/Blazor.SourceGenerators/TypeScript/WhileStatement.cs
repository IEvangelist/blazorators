// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class WhileStatement : IterationStatement
{
    internal WhileStatement() => ((INode)this).Kind = SyntaxKind.WhileStatement;

    internal IExpression Expression { get; set; }
}