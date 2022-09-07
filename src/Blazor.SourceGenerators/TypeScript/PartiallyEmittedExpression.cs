// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PartiallyEmittedExpression : LeftHandSideExpression
{
    internal PartiallyEmittedExpression() => ((INode)this).Kind = CommentKind.PartiallyEmittedExpression;

    internal IExpression Expression { get; set; }
}