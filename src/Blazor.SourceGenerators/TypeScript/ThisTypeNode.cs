// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ThisTypeNode : TypeNode
{
    internal ThisTypeNode() => ((INode)this).Kind = CommentKind.ThisType;
}