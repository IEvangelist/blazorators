// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DefaultClause : Node, ICaseOrDefaultClause
{
    internal DefaultClause() => ((INode)this).Kind = CommentKind.DefaultClause;

    internal NodeArray<IStatement> Statements { get; set; }
}