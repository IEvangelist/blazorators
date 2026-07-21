// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.JSInterop;

internal sealed class DomUnionDeliveryHandler<TResult>(
    IReadOnlyList<DomUnionInboundArm<TResult>> arms) : IAsyncDisposable
{
    private readonly object _gate = new();
    private bool _received;
    private bool _closed;
    private int _armIndex;
    private JsonElement? _json;
    private IJSObjectReference? _reference;

    [JSInvokable("ReceiveUnionJson")]
    public Task<bool> ReceiveJsonAsync(int armIndex, JsonElement value)
    {
        lock (_gate)
        {
            if (_closed || _received || !IsJsonArm(armIndex))
                return Task.FromResult(false);
            _received = true;
            _armIndex = armIndex;
            _json = value.Clone();
            return Task.FromResult(true);
        }
    }

    [JSInvokable("ReceiveUnionReference")]
    public async Task<bool> ReceiveReferenceAsync(
        int armIndex,
        IJSObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        lock (_gate)
        {
            if (!_closed && !_received && IsReferenceArm(armIndex))
            {
                _received = true;
                _armIndex = armIndex;
                _reference = reference;
                return true;
            }
        }
        await DomReferenceDeliveryHandler.DisposeReferenceAsync(reference)
            .ConfigureAwait(false);
        return false;
    }

    public async ValueTask<TResult> TakeResultAsync()
    {
        JsonElement? json;
        IJSObjectReference? reference;
        DomUnionInboundArm<TResult> arm;
        lock (_gate)
        {
            _closed = true;
            if (!_received)
            {
                throw new DomTransportException(
                    "JavaScript completed union transport without selecting an arm.");
            }
            arm = arms[_armIndex];
            json = _json;
            reference = _reference;
            _reference = null;
        }

        if (json is not null)
            return arm.JsonFactory!(json.Value);
        try
        {
            return arm.ReferenceFactory!(reference!);
        }
        catch
        {
            await DomReferenceDeliveryHandler.DisposeReferenceAsync(reference!)
                .ConfigureAwait(false);
            throw;
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
            await DomReferenceDeliveryHandler.DisposeReferenceAsync(reference)
                .ConfigureAwait(false);
        }
    }

    private bool IsJsonArm(int index) =>
        index >= 0 && index < arms.Count && arms[index].JsonFactory is not null;

    private bool IsReferenceArm(int index) =>
        index >= 0 && index < arms.Count && arms[index].ReferenceFactory is not null;
}
