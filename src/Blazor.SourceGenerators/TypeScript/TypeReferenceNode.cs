// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeReferenceNode : TypeNode
{
    internal TypeReferenceNode() => ((INode)this).Kind = CommentKind.TypeReference;

    internal IEntityName TypeName { get; set; }
    internal NodeArray<ITypeNode> TypeArguments { get; set; }
}