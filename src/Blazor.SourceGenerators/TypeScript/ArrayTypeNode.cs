// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ArrayTypeNode : TypeNode
{
    internal ArrayTypeNode() => ((INode)this).Kind = SyntaxKind.ArrayType;

    internal ITypeNode ElementType { get; set; } = default!;
}