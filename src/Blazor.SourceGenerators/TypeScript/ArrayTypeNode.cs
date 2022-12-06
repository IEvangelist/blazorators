// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ArrayTypeNode : TypeNode
{
    public ArrayTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.ArrayType;

    public ITypeNode ElementType? { get; set; }
}