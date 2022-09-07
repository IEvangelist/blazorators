// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeOperatorNode : ParenthesizedTypeNode
{
    internal TypeOperatorNode() => ((INode)this).Kind = CommentKind.TypeOperator;

    internal CommentKind Operator { get; set; } = CommentKind.KeyOfKeyword;
}