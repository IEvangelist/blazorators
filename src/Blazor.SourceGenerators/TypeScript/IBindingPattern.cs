// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IBindingPattern : INode
{
    internal NodeArray<IArrayBindingElement> Elements { get; set; }
}
