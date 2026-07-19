// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Base class for all generated DOM proxy types.  Holds the underlying JS
/// reference, the async dispatch runtime, and the proxy factory used to
/// create child proxies.  Disposing the proxy disposes its owned JS reference.
/// </summary>
public abstract class DomProxyBase : IDomProxy
{
    private int _disposed;

    /// <summary>The underlying JS object reference for this proxy.</summary>
    public IJSObjectReference Reference { get; }

    /// <summary>Async dispatch runtime.</summary>
    protected IDomRuntime Runtime { get; }

    /// <summary>Proxy factory for creating child proxies from returned references.</summary>
    protected IDomProxyFactory Factory { get; }

    /// <param name="reference">Owned JS object reference.</param>
    /// <param name="runtime">DOM runtime for dispatch.</param>
    /// <param name="factory">Proxy factory for wrapping child references.</param>
    protected DomProxyBase(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Runtime   = runtime   ?? throw new ArgumentNullException(nameof(runtime));
        Factory   = factory   ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Disposes the underlying JS reference.  Idempotent; safe to call
    /// multiple times or concurrently.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                await Reference.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Circuit torn down — reference is already invalid.
            }
        }
    }
}
