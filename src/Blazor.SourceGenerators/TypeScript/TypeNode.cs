// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class TypeNode : Node, ITypeNode
{
    object ITypeNode.TypeNodeBrand { get; set; } = default!;
}