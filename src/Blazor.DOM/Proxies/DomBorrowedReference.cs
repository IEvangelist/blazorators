// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// A callback-scoped typed proxy. It is released after the awaited handler unless
/// <see cref="Promote"/> transfers ownership to the handler.
/// </summary>
public sealed class DomBorrowedReference<TProxy> where TProxy : class, IDomProxy
{
    private readonly object _gate = new();
    private readonly TProxy _proxy;
    private int _state;

    internal DomBorrowedReference(TProxy proxy) =>
        _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));

    /// <summary>The typed live proxy valid for the callback scope.</summary>
    public TProxy Proxy
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_state == 2, this);
                return _proxy;
            }
        }
    }

    /// <summary>
    /// Promotes the borrowed proxy to owned lifetime. Promotion is committed only
    /// when the awaited callback completes successfully.
    /// </summary>
    public TProxy Promote()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_state == 2, this);
            _state = 1;
            return _proxy;
        }
    }

    internal async ValueTask CompleteAsync(bool callbackSucceeded)
    {
        var release = false;
        lock (_gate)
        {
            if (_state == 2)
            {
                return;
            }
            if (!callbackSucceeded || _state == 0)
            {
                _state = 2;
                release = true;
            }
        }
        if (release)
        {
            await _proxy.DisposeAsync().ConfigureAwait(false);
        }
    }
}
