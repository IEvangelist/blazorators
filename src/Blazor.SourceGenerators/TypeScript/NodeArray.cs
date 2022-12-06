// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class NodeArray<T> : List<T>, ITextRange
{
    public bool HasTrailingComma { get; set; }
    public TransformFlags TransformFlags { get; set; }
    int? ITextRange.Pos { get; set; }
    int? ITextRange.End { get; set; }

    public NodeArray()
    {
    }

    public NodeArray(T[] elements) : base(elements)
    {
    }

    public static NodeArray<T> Empty => new(Array.Empty<T>());
}
