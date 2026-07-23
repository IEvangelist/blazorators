// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomReferencePairCallbackHandler<TFirst, TSecond> : IDisposable
    where TFirst : class, IDomProxy
    where TSecond : class, IDomProxy
{
    private readonly IDomProxyFactory _factory;
    private readonly DomTransportDescriptor _firstTransport;
    private readonly DomTransportDescriptor _secondTransport;
    private readonly Func<
        DomBorrowedReference<TFirst>,
        DomBorrowedReference<TSecond>,
        Task> _callback;
    private int _disposed;

    public DomReferencePairCallbackHandler(
        IDomProxyFactory factory,
        DomTransportDescriptor firstTransport,
        DomTransportDescriptor secondTransport,
        Func<
            DomBorrowedReference<TFirst>,
            DomBorrowedReference<TSecond>,
            Task> callback)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _firstTransport = firstTransport
            ?? throw new ArgumentNullException(nameof(firstTransport));
        _secondTransport = secondTransport
            ?? throw new ArgumentNullException(nameof(secondTransport));
        _firstTransport.RequireReference(nameof(firstTransport));
        _secondTransport.RequireReference(nameof(secondTransport));
        if (_firstTransport.Nullable || _secondTransport.Nullable)
        {
            throw new ArgumentException(
                "Persistent callback reference arguments must be non-nullable.");
        }
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    [JSInvokable("HandleReferencePair")]
    public async Task<bool> HandleReferencePairAsync(
        IJSObjectReference firstReference,
        IJSObjectReference secondReference)
    {
        ArgumentNullException.ThrowIfNull(firstReference);
        ArgumentNullException.ThrowIfNull(secondReference);
        if (Volatile.Read(ref _disposed) != 0)
        {
            try
            {
                await DisposeReferenceAsync(firstReference).ConfigureAwait(false);
            }
            finally
            {
                await DisposeReferenceAsync(secondReference).ConfigureAwait(false);
            }
            return false;
        }

        DomBorrowedReference<TFirst>? first = null;
        DomBorrowedReference<TSecond>? second = null;
        var succeeded = false;
        try
        {
            first = new(_factory.Create<TFirst>(firstReference));
            second = new(_factory.Create<TSecond>(secondReference));
            await _callback(first, second).ConfigureAwait(false);
            succeeded = Volatile.Read(ref _disposed) == 0;
            return succeeded;
        }
        finally
        {
            try
            {
                await CompleteReferenceAsync(first, firstReference, succeeded)
                    .ConfigureAwait(false);
            }
            finally
            {
                await CompleteReferenceAsync(second, secondReference, succeeded)
                    .ConfigureAwait(false);
            }
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    private static async ValueTask CompleteReferenceAsync<TProxy>(
        DomBorrowedReference<TProxy>? borrowed,
        IJSObjectReference reference,
        bool callbackSucceeded)
        where TProxy : class, IDomProxy
    {
        if (borrowed is not null)
        {
            await borrowed.CompleteAsync(callbackSucceeded).ConfigureAwait(false);
            return;
        }

        await DisposeReferenceAsync(reference).ConfigureAwait(false);
    }

    private static async ValueTask DisposeReferenceAsync(IJSObjectReference reference)
    {
        try
        {
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}
