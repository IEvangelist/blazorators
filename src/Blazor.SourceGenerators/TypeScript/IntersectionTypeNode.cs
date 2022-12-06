// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class IntersectionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    public IntersectionTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.IntersectionType;

    public NodeArray<ITypeNode> Types { get; set; }
}