// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DomStreamCallbackHandler
{
    private readonly object _gate = new();
    private IJSStreamReference? _reference;
    private bool _hasValue;
    private bool _received;
    private bool _closed;

    [JSInvokable("ReceiveStream")]
    public async Task<bool> ReceiveStreamAsync(
        IJSStreamReference? reference,
        long length,
        bool hasValue)
    {
        var accepted = false;
        var invalid = false;
        lock (_gate)
        {
            if (_closed || _received)
            {
                accepted = false;
            }
            else if (length < 0 ||
                (!hasValue && (length != 0 || reference is not null)) ||
                (hasValue && (length == 0) != (reference is null)))
            {
                invalid = true;
            }
            else
            {
                _received = true;
                _reference = reference;
                _hasValue = hasValue;
                accepted = true;
            }
        }
        if (!accepted && reference is not null)
        {
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        if (invalid)
        {
            throw new DomTransportException(
                "JavaScript supplied invalid stream metadata.");
        }
        return accepted;
    }

    public DomStreamCallbackResult TakeResult()
    {
        lock (_gate)
        {
            _closed = true;
            if (!_received)
            {
                throw new DomTransportException(
                    "JavaScript completed stream registration without supplying stream metadata.");
            }
            var reference = _reference;
            _reference = null;
            return new(reference, _hasValue);
        }
    }

    public async ValueTask DisposeAsync()
    {
        IJSStreamReference? reference;
        lock (_gate)
        {
            _closed = true;
            reference = _reference;
            _reference = null;
        }
        if (reference is not null)
        {
            await reference.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal readonly record struct DomStreamCallbackResult(
        IJSStreamReference? Reference,
        bool HasValue);
}
