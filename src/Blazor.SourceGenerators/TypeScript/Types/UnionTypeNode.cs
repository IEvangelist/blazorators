// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class UnionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    public UnionTypeNode()
    {
        Kind = TypeScriptSyntaxKind.UnionType;
    }

    public NodeArray<ITypeNode> Types { get; set; }
}