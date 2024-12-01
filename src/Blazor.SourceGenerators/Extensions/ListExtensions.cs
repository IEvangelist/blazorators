// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class ListExtensions
{
    internal static IEnumerable<(Iteration Index, T Item)> Select<T>(this List<T> list)
    {
        var count = list.Count;
        for (var i = 0; i < count; ++i)
        {
            yield return (new(i, count), list[i]);
        }
    }
}

readonly record struct Iteration(
    int Index,
    int Count)
{
    internal bool IsFirst => Index is 0;
    internal bool IsLast => Index == Count - 1;
    internal bool HasMore => Index + 1 < Count;
}
