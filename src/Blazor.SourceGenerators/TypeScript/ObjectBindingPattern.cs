// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ObjectBindingPattern : Node, IBindingPattern
{
    public ObjectBindingPattern() => ((INode)this).Kind = TypeScriptSyntaxKind.ObjectBindingPattern;

    NodeArray<IArrayBindingElement> IBindingPattern.Elements? { get; set; }
}
