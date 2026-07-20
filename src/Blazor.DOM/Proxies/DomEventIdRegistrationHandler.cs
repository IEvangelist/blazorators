// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomEventIdRegistrationHandler
{
    private readonly object _gate = new();
    private int? _listenerId;
    private bool _closed;

    [JSInvokable("ReceiveRegistration")]
    public Task<bool> ReceiveRegistrationAsync(int listenerId)
    {
        if (listenerId <= 0)
        {
            throw new DomTransportException(
                $"JavaScript supplied invalid event listener ID {listenerId}.");
        }
        lock (_gate)
        {
            if (_closed || _listenerId is not null)
            {
                return Task.FromResult(false);
            }
            _listenerId = listenerId;
        }
        return Task.FromResult(true);
    }

    public int TakeRegistration()
    {
        lock (_gate)
        {
            _closed = true;
            var listenerId = _listenerId;
            _listenerId = null;
            return listenerId ??
                throw new DomTransportException(
                    "JavaScript completed event registration without supplying a listener ID.");
        }
    }

    public int? CloseAndTakeRegistration()
    {
        lock (_gate)
        {
            _closed = true;
            var listenerId = _listenerId;
            _listenerId = null;
            return listenerId;
        }
    }
}
