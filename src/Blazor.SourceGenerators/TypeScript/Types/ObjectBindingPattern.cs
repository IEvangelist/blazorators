// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ObjectBindingPattern : Node, IBindingPattern
{
    public ObjectBindingPattern()
    {
        Kind = TypeScriptSyntaxKind.ObjectBindingPattern;
    }

    public NodeArray<IArrayBindingElement> Elements { get; set; }
}