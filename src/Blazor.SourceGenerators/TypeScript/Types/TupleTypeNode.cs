// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TupleTypeNode : TypeNode
{
    public TupleTypeNode()
    {
        Kind = TypeScriptSyntaxKind.TupleType;
    }

    public NodeArray<ITypeNode> ElementTypes { get; set; }
}