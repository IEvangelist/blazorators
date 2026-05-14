// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class EnumerableExtensions
{
    /// <summary>
    /// Iteratively flattens a tree of values into a depth-first sequence, using
    /// <paramref name="keySelector"/> to detect cycles. Each item is yielded at most
    /// once. Returns an empty sequence when <paramref name="source"/> is <c>null</c>.
    /// </summary>
    internal static IEnumerable<T> Flatten<T, TKey>(
        this IEnumerable<T>? source,
        Func<T, IEnumerable<T>?> childSelector,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        if (source is null)
        {
            yield break;
        }

        var visited = new HashSet<TKey>(keyComparer);
        var stack = new Stack<T>();
        foreach (var item in source)
        {
            stack.Push(item);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(keySelector(current)))
            {
                continue;
            }

            yield return current;

            var children = childSelector(current);
            if (children is null)
            {
                continue;
            }

            foreach (var child in children)
            {
                stack.Push(child);
            }
        }
    }
}
