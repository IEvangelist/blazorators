// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NotEmittedStatement : Statement
{
    internal NotEmittedStatement() => ((INode)this).Kind = CommentKind.NotEmittedStatement;
}