// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TupleTypeNode : TypeNode
{
    public TupleTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TupleType;

    public NodeArray<ITypeNode> ElementTypes { get; set; }
}