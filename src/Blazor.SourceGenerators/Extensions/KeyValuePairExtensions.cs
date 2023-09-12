// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class KeyValuePairExtensions
{
    /// <summary>
    /// Extends the <see cref="KeyValuePair{TKey, TValue}"/> type,
    /// by exposing the <c>Deconstruct</c> functionality. Meaning that
    /// we can now do the following:
    /// <c>foreach (var (key, value) in Dictionary) { ... }</c>
    /// </summary>
    /// <typeparam name="TKey">The key <c>TKey</c> type.</typeparam>
    /// <typeparam name="TValue">The value <c>TValue</c> type.</typeparam>
    internal static void Deconstruct<TKey, TValue>(
        this KeyValuePair<TKey, TValue> kvp,
        out TKey key,
        out TValue value) => (key, value) = (kvp.Key, kvp.Value);
}
