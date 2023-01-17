// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeQueryNode : TypeNode
{
    public TypeQueryNode()
    {
        Kind = TypeScriptSyntaxKind.TypeQuery;
    }

    public IEntityName ExprName { get; set; }
}