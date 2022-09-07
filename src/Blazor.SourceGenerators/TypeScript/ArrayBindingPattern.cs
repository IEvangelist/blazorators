// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ArrayBindingPattern : Node, IBindingPattern
{
    internal ArrayBindingPattern() => ((INode)this).Kind = CommentKind.ArrayBindingPattern;

    NodeArray<IArrayBindingElement> IBindingPattern.Elements { get; set; } = default!;
}
