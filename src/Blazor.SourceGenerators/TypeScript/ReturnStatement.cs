// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ReturnStatement : Statement
{
    internal ReturnStatement() => ((INode)this).Kind = CommentKind.ReturnStatement;

    internal IExpression Expression { get; set; }
}