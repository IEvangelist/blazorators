// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed record DomCallbackResult<TResult>(bool Accepted, TResult? Result);

internal sealed class DomReferenceResultCallbackHandler<TProxy, TResult> : IDisposable
    where TProxy : class, IDomProxy
{
    private readonly IDomProxyFactory _factory;
    private readonly DomTransportDescriptor _transport;
    private readonly Func<DomBorrowedReference<TProxy>?, Task<TResult>> _callback;
    private int _disposed;

    public DomReferenceResultCallbackHandler(
        IDomProxyFactory factory,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task<TResult>> callback)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _transport.RequireReference(nameof(transport));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    [JSInvokable("HandleReferenceResult")]
    public async Task<DomCallbackResult<TResult>> HandleReferenceResultAsync(
        IJSObjectReference? reference)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            if (reference is not null)
                await DisposeReferenceAsync(reference).ConfigureAwait(false);
            return new(false, default);
        }
        if (reference is null)
        {
            if (!_transport.Nullable)
            {
                throw new DomTransportException(
                    $"JavaScript supplied null for non-nullable '{_transport.SourceType}'.");
            }
            var result = await _callback(null).ConfigureAwait(false);
            return new(Volatile.Read(ref _disposed) == 0, result);
        }

        TProxy proxy;
        try
        {
            proxy = _factory.Create<TProxy>(reference);
        }
        catch
        {
            await DisposeReferenceAsync(reference).ConfigureAwait(false);
            throw;
        }

        var borrowed = new DomBorrowedReference<TProxy>(proxy);
        var succeeded = false;
        try
        {
            var result = await _callback(borrowed).ConfigureAwait(false);
            succeeded = Volatile.Read(ref _disposed) == 0;
            return new(succeeded, result);
        }
        finally
        {
            await borrowed.CompleteAsync(succeeded).ConfigureAwait(false);
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

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
