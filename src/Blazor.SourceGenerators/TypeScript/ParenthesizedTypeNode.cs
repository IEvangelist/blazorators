// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ParenthesizedTypeNode : TypeNode
{
    internal ParenthesizedTypeNode() => ((INode)this).Kind = CommentKind.ParenthesizedType;

    internal ITypeNode Type { get; set; }
}