// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class UnionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    public UnionTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.UnionType;

    public NodeArray<ITypeNode> Types { get; set; }
}