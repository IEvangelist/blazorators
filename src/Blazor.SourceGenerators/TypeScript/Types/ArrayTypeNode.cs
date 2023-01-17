// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ArrayTypeNode : TypeNode
{
    public ArrayTypeNode()
    {
        Kind = TypeScriptSyntaxKind.ArrayType;
    }

    public ITypeNode ElementType { get; set; }
}