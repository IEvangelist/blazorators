// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class UnionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    internal UnionTypeNode() => ((INode)this).Kind = CommentKind.UnionType;

    internal NodeArray<ITypeNode> Types { get; set; }
}