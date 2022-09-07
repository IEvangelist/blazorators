// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SwitchStatement : Statement
{
    internal SwitchStatement() => ((INode)this).Kind = CommentKind.SwitchStatement;

    internal IExpression Expression { get; set; }
    internal CaseBlock CaseBlock { get; set; }
    internal bool PossiblyExhaustive { get; set; }
}