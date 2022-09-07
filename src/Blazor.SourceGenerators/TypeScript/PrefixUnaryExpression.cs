// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PrefixUnaryExpression : IncrementExpression
{
    internal PrefixUnaryExpression() => ((INode)this).Kind = CommentKind.PrefixUnaryExpression;

    internal CommentKind Operator { get; set; }
    internal IExpression Operand { get; set; }
}