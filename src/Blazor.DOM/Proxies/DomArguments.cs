// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Unwrapping helpers for argument arrays passed to JS interop calls.
/// Converts <see cref="IDomProxy"/> instances to their underlying
/// <see cref="IJSObjectReference"/> so Blazor can marshal them correctly.
/// </summary>
public static class DomArguments
{
    /// <summary>
    /// Returns a new array in which every <see cref="IDomProxy"/> element has
    /// been replaced with its <see cref="IDomProxy.Reference"/>, and all other
    /// elements are passed through unchanged.  Returns <see langword="null"/>
    /// when <paramref name="args"/> is <see langword="null"/>.
    /// </summary>
    public static object?[]? Unwrap(object?[]? args)
    {
        if (args is null or { Length: 0 }) return args;

        object?[]? result = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is IDomProxy proxy)
            {
                result ??= (object?[])args.Clone();
                result[i] = proxy.Reference;
            }
        }
        return result ?? args;
    }
}
