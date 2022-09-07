// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DoStatement : IterationStatement
{
    internal DoStatement() => ((INode)this).Kind = CommentKind.DoStatement;

    internal IExpression Expression { get; set; }
}