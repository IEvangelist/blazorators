// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ObjectBindingPattern : Node, IBindingPattern
{
    internal ObjectBindingPattern() => ((INode)this).Kind = CommentKind.ObjectBindingPattern;

    NodeArray<IArrayBindingElement> IBindingPattern.Elements { get; set; } = default!;
}
