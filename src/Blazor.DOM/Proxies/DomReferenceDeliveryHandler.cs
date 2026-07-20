// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomReferenceDeliveryHandler : IAsyncDisposable
{
    private readonly object _gate = new();
    private IJSObjectReference? _reference;
    private bool _received;
    private bool _closed;

    [JSInvokable("ReceiveReference")]
    public async Task<bool> ReceiveReferenceAsync(IJSObjectReference? reference)
    {
        var accepted = false;
        lock (_gate)
        {
            if (!_closed && !_received)
            {
                _received = true;
                _reference = reference;
                accepted = true;
            }
        }

        if (!accepted && reference is not null)
        {
            await DisposeReferenceAsync(reference).ConfigureAwait(false);
        }
        return accepted;
    }

    public IJSObjectReference? TakeReference()
    {
        lock (_gate)
        {
            _closed = true;
            if (!_received)
            {
                throw new DomTransportException(
                    "JavaScript completed reference transport without supplying a value.");
            }
            var reference = _reference;
            _reference = null;
            return reference;
        }
    }

    public async ValueTask DisposeAsync()
    {
        IJSObjectReference? reference;
        lock (_gate)
        {
            _closed = true;
            reference = _reference;
            _reference = null;
        }
        if (reference is not null)
        {
            await DisposeReferenceAsync(reference).ConfigureAwait(false);
        }
    }

    internal static async ValueTask DisposeReferenceAsync(
        IJSObjectReference reference)
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
