// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class NodeArray<T> : List<T>, ITextRange
{
    internal bool HasTrailingComma { get; set; }
    internal TransformFlags TransformFlags { get; set; }
    int? ITextRange.Pos { get; set; }
    int? ITextRange.End { get; set; }

    internal NodeArray()
    {
    }

    internal NodeArray(T[] elements) : base(elements)
    {
    }
}
