// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DebuggerStatement : Statement
{
    internal DebuggerStatement() => ((INode)this).Kind = CommentKind.DebuggerStatement;
}