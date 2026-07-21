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
            await DisposeReferenceAsync(firstReference).ConfigureAwait(false);
            await DisposeReferenceAsync(secondReference).ConfigureAwait(false);
            return false;
        }

        var first = CreateBorrowed<TFirst>(firstReference);
        DomBorrowedReference<TSecond>? second = null;
        var succeeded = false;
        try
        {
            second = CreateBorrowed<TSecond>(secondReference);
            await _callback(first, second).ConfigureAwait(false);
            succeeded = Volatile.Read(ref _disposed) == 0;
            return succeeded;
        }
        finally
        {
            await first.CompleteAsync(succeeded).ConfigureAwait(false);
            if (second is not null)
                await second.CompleteAsync(succeeded).ConfigureAwait(false);
            else
                await DisposeReferenceAsync(secondReference).ConfigureAwait(false);
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    private DomBorrowedReference<TProxy> CreateBorrowed<TProxy>(
        IJSObjectReference reference)
        where TProxy : class, IDomProxy
    {
        try
        {
            return new(_factory.Create<TProxy>(reference));
        }
        catch
        {
            DisposeReferenceAsync(reference).AsTask().GetAwaiter().GetResult();
            throw;
        }
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
