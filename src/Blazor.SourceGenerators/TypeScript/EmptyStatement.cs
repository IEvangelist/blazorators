// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EmptyStatement : Statement
{
    internal EmptyStatement() => ((INode)this).Kind = CommentKind.EmptyStatement;
}