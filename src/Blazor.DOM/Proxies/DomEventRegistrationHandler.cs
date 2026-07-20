// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.ExceptionServices;

namespace Microsoft.JSInterop;

internal sealed class DomEventRegistrationHandler
{
    private readonly object _gate = new();
    private IJSObjectReference? _registration;
    private bool _received;
    private bool _closed;

    [JSInvokable("ReceiveRegistration")]
    public async Task<bool> ReceiveRegistrationAsync(
        IJSObjectReference? registration)
    {
        var accepted = false;
        lock (_gate)
        {
            if (!_closed && !_received && registration is not null)
            {
                _received = true;
                _registration = registration;
                accepted = true;
            }
        }
        if (!accepted && registration is not null)
        {
            await DisposeRegistrationAsync(registration).ConfigureAwait(false);
        }
        return accepted;
    }

    public IJSObjectReference TakeRegistration()
    {
        lock (_gate)
        {
            _closed = true;
            var registration = _registration;
            _registration = null;
            if (!_received || registration is null)
            {
                throw new DomTransportException(
                    "JavaScript completed event registration without supplying a reference.");
            }
            return registration;
        }
    }

    public async ValueTask DisposeAsync()
    {
        IJSObjectReference? registration;
        lock (_gate)
        {
            _closed = true;
            registration = _registration;
            _registration = null;
        }
        if (registration is not null)
        {
            await DisposeRegistrationAsync(registration).ConfigureAwait(false);
        }
    }

    internal static async ValueTask DisposeRegistrationAsync(
        IJSObjectReference registration)
    {
        Exception? failure = null;
        try
        {
            await registration.InvokeVoidAsync("dispose").ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        try
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            failure ??= ex;
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
