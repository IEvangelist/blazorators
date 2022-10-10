// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeQueryNode : TypeNode
{
    internal TypeQueryNode() => ((INode)this).Kind = SyntaxKind.TypeQuery;

    internal IEntityName ExprName { get; set; }
}