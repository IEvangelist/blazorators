// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IndexedAccessTypeNode : TypeNode
{
    public IndexedAccessTypeNode()
    {
        Kind = TypeScriptSyntaxKind.IndexedAccessType;
    }

    public ITypeNode ObjectType { get; set; }
    public ITypeNode IndexType { get; set; }
}