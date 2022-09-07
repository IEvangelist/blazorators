// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class IntersectionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    internal IntersectionTypeNode() => ((INode)this).Kind = CommentKind.IntersectionType;

    internal NodeArray<ITypeNode> Types { get; set; }
}