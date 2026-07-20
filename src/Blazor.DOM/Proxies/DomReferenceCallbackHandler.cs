// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomReferenceCallbackHandler<TProxy> : IDisposable
    where TProxy : class, IDomProxy
{
    private readonly IDomProxyFactory _factory;
    private readonly DomTransportDescriptor _transport;
    private readonly Func<DomBorrowedReference<TProxy>?, Task> _callback;
    private int _disposed;

    public DomReferenceCallbackHandler(
        IDomProxyFactory factory,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task> callback)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _transport.RequireReference(nameof(transport));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    [JSInvokable("HandleReference")]
    public async Task<bool> HandleReferenceAsync(IJSObjectReference? reference)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            if (reference is not null)
            {
                await DisposeReferenceAsync(reference).ConfigureAwait(false);
            }
            return false;
        }
        if (reference is null)
        {
            if (!_transport.Nullable)
            {
                throw new DomTransportException(
                    $"JavaScript supplied null for non-nullable '{_transport.SourceType}'.");
            }
            await _callback(null).ConfigureAwait(false);
            return Volatile.Read(ref _disposed) == 0;
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
            await _callback(borrowed).ConfigureAwait(false);
            succeeded = Volatile.Read(ref _disposed) == 0;
            return succeeded;
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
