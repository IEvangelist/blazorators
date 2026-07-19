// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Represents a live DOM event subscription.  Dispose to remove the underlying
/// JS event listener and release the dotnet callback reference.
/// Disposal is idempotent and thread-safe.
/// </summary>
/// <remarks>
/// Server and shared runtimes require an asynchronous round-trip to the JS
/// side to remove the listener.  Always use <c>await using</c> (i.e.
/// <see cref="DisposeAsync"/>) so cleanup actually completes before the
/// call-site continues.  Synchronous <see cref="IDisposable"/> is intentionally
/// absent to prevent fire-and-forget disposal patterns that silently leave
/// listeners alive.
/// </remarks>
public sealed class DomEventSubscription : IAsyncDisposable
{
    private readonly IDomRuntime _runtime;
    private readonly int _listenerId;
    private readonly DotNetObjectReference<DomCallbackHandler> _handlerRef;
    private int _disposed;

    internal DomEventSubscription(
        IDomRuntime runtime,
        int listenerId,
        DotNetObjectReference<DomCallbackHandler> handlerRef)
    {
        _runtime = runtime;
        _listenerId = listenerId;
        _handlerRef = handlerRef;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _runtime.RemoveEventListenerAsync(_listenerId).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down — listener is already gone; swallow only this.
        }
        catch (OperationCanceledException)
        {
            // Cancellation during teardown is acceptable.
        }
        finally
        {
            _handlerRef.Value.Dispose();
            _handlerRef.Dispose();
        }
    }
}
