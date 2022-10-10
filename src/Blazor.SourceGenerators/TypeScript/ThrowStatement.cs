// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ThrowStatement : Statement
{
    internal ThrowStatement() => ((INode)this).Kind = SyntaxKind.ThrowStatement;

    internal IExpression Expression { get; set; }
}