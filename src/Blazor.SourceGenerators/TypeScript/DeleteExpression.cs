// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DeleteExpression : UnaryExpression
{
    internal DeleteExpression() => ((INode)this).Kind = CommentKind.DeleteExpression;

    internal IExpression Expression { get; set; } = default!;
}