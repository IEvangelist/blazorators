// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IBindingPattern : INode
{
    public NodeArray<IArrayBindingElement> Elements { get; set; }
}
