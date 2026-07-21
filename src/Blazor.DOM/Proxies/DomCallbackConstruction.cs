// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Holds a constructed JavaScript reference and the managed callback resource
/// whose lifetime must be transferred to the resulting proxy.
/// </summary>
public sealed class DomCallbackConstruction : IDisposable
{
    private IDisposable? _callbackResource;

    internal DomCallbackConstruction(
        IJSObjectReference reference,
        IDisposable callbackResource)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _callbackResource = callbackResource
            ?? throw new ArgumentNullException(nameof(callbackResource));
    }

    /// <summary>The constructed JavaScript object reference.</summary>
    public IJSObjectReference Reference { get; }

    internal IDisposable TakeCallbackResource() =>
        Interlocked.Exchange(ref _callbackResource, null)
        ?? throw new InvalidOperationException(
            "Callback resource ownership has already been transferred.");

    /// <inheritdoc />
    public void Dispose() =>
        Interlocked.Exchange(ref _callbackResource, null)?.Dispose();
}
