// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// A named wrapper around a <see cref="IJSObjectReference"/> with explicit
/// ownership semantics.  Use <see cref="Owned"/> for references that should
/// be disposed together with this wrapper, and <see cref="Shared"/> for
/// global / module references that outlive individual proxies.
/// Disposal is idempotent and thread-safe.
/// </summary>
public sealed class DomObjectReference : IDomProxy
{
    private readonly bool _owned;
    private int _disposed;

    /// <summary>The underlying JS object reference.</summary>
    public IJSObjectReference Reference { get; }

    private DomObjectReference(IJSObjectReference reference, bool owned)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _owned = owned;
    }

    /// <summary>
    /// Creates a <see cref="DomObjectReference"/> that will dispose
    /// <paramref name="reference"/> when it is itself disposed.
    /// </summary>
    public static DomObjectReference Owned(IJSObjectReference reference) =>
        new(reference, owned: true);

    /// <summary>
    /// Creates a <see cref="DomObjectReference"/> that will <em>not</em>
    /// dispose <paramref name="reference"/> when it is itself disposed.
    /// Use for shared globals or module references.
    /// </summary>
    public static DomObjectReference Shared(IJSObjectReference reference) =>
        new(reference, owned: false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _owned)
        {
            await Reference.DisposeAsync().ConfigureAwait(false);
        }
    }
}
