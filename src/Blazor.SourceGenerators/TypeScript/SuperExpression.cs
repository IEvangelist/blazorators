// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SuperExpression : PrimaryExpression
{
    internal SuperExpression() => ((INode)this).Kind = CommentKind.SuperKeyword;
}