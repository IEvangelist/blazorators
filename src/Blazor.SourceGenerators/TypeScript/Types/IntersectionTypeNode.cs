// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IntersectionTypeNode : TypeNode, IUnionOrIntersectionTypeNode
{
    public IntersectionTypeNode()
    {
        Kind = TypeScriptSyntaxKind.IntersectionType;
    }

    public NodeArray<ITypeNode> Types { get; set; }
}