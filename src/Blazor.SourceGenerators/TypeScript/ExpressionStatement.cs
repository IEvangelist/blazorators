// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExpressionStatement : Statement
{
    internal ExpressionStatement() => ((INode)this).Kind = SyntaxKind.ExpressionStatement;

    internal IExpression Expression { get; set; }
}