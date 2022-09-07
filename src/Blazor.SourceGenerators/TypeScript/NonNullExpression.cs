// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NonNullExpression : /*LeftHandSideExpression*/MemberExpression
{
    internal NonNullExpression() => ((INode)this).Kind = CommentKind.NonNullExpression;

    internal IExpression Expression { get; set; }
}