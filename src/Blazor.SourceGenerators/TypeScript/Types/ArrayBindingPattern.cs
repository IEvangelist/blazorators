// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ArrayBindingPattern : Node, IBindingPattern
{
    public ArrayBindingPattern()
    {
        Kind = TypeScriptSyntaxKind.ArrayBindingPattern;
    }

    public NodeArray<IArrayBindingElement> Elements { get; set; }
}