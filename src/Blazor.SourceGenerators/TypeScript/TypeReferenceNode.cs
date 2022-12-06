// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeReferenceNode : TypeNode
{
    public TypeReferenceNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeReference;

    public IEntityName TypeName { get; set; }
    public NodeArray<ITypeNode> TypeArguments { get; set; }
}