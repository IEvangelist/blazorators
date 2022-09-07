// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TupleTypeNode : TypeNode
{
    internal TupleTypeNode() => ((INode)this).Kind = CommentKind.TupleType;

    internal NodeArray<ITypeNode> ElementTypes { get; set; }
}