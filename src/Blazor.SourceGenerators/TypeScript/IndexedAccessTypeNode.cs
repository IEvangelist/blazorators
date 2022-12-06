// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class IndexedAccessTypeNode : TypeNode
{
    public IndexedAccessTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.IndexedAccessType;

    public ITypeNode ObjectType { get; set; }
    public ITypeNode IndexType { get; set; }
}