// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ArrayBindingPattern : Node, IBindingPattern
{
    public ArrayBindingPattern() => ((INode)this).Kind = TypeScriptSyntaxKind.ArrayBindingPattern;

    NodeArray<IArrayBindingElement> IBindingPattern.Elements? { get; set; }
}
