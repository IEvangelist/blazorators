// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class EnumerableExtensions
{
    internal static IEnumerable<T> Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> childSelector) =>
        source?.SelectMany(child => childSelector(child).Flatten(childSelector)).Concat(source) ?? [];
}
