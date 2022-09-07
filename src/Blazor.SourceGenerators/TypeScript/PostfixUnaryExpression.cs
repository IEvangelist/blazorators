// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PostfixUnaryExpression : IncrementExpression
{
    internal PostfixUnaryExpression() => ((INode)this).Kind = CommentKind.PostfixUnaryExpression;

    internal IExpression Operand { get; set; } = default!;
    internal CommentKind Operator { get; set; } = default!;
}