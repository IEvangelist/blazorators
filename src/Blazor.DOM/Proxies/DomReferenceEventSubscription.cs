// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Owns a typed event-listener registration and its managed callback reference.
/// </summary>
public sealed class DomReferenceEventSubscription<TProxy> : IAsyncDisposable
    where TProxy : class, IDomProxy
{
    private readonly IJSObjectReference _registration;
    private readonly DotNetObjectReference<DomReferenceCallbackHandler<TProxy>> _handlerReference;
    private int _disposed;

    internal DomReferenceEventSubscription(
        IJSObjectReference registration,
        DotNetObjectReference<DomReferenceCallbackHandler<TProxy>> handlerReference)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _handlerReference = handlerReference ??
            throw new ArgumentNullException(nameof(handlerReference));
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
            await DomEventRegistrationHandler
                .DisposeRegistrationAsync(_registration)
                .ConfigureAwait(false);
        }
        finally
        {
            _handlerReference.Value.Dispose();
            _handlerReference.Dispose();
        }
    }
}
