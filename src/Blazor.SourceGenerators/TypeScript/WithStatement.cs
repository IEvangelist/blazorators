// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class WithStatement : Statement
{
    internal WithStatement() => ((INode)this).Kind = CommentKind.WithStatement;

    internal IExpression Expression { get; set; }
    internal IStatement Statement { get; set; }
}