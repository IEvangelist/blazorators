// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeQueryNode : TypeNode
{
    public TypeQueryNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeQuery;

    public IEntityName ExprName { get; set; }
}