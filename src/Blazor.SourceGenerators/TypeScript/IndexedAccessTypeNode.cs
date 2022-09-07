// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class IndexedAccessTypeNode : TypeNode
{
    internal IndexedAccessTypeNode() => ((INode)this).Kind = CommentKind.IndexedAccessType;

    internal ITypeNode ObjectType { get; set; }
    internal ITypeNode IndexType { get; set; }
}