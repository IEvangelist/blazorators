// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomCallbackRegistrationHandler
{
    private readonly object _gate = new();
    private int? _registrationId;
    private bool _closed;

    [JSInvokable("ReceiveRegistration")]
    public Task<bool> ReceiveRegistrationAsync(int registrationId)
    {
        if (registrationId <= 0)
        {
            throw new DomTransportException(
                $"JavaScript supplied invalid callback registration ID {registrationId}.");
        }
        lock (_gate)
        {
            if (_closed || _registrationId is not null)
                return Task.FromResult(false);
            _registrationId = registrationId;
        }
        return Task.FromResult(true);
    }

    public int TakeRegistration()
    {
        lock (_gate)
        {
            _closed = true;
            var registrationId = _registrationId;
            _registrationId = null;
            return registrationId
                ?? throw new DomTransportException(
                    "JavaScript completed callback registration without supplying an ID.");
        }
    }

    public int? CloseAndTakeRegistration()
    {
        lock (_gate)
        {
            _closed = true;
            var registrationId = _registrationId;
            _registrationId = null;
            return registrationId;
        }
    }
}
