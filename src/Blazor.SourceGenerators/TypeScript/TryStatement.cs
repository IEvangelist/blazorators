// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TryStatement : Statement
{
    internal TryStatement() => ((INode)this).Kind = CommentKind.TryStatement;

    internal Block TryBlock { get; set; }
    internal CatchClause CatchClause { get; set; }
    internal Block FinallyBlock { get; set; }
}