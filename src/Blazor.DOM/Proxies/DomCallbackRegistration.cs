// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Owns a persistent JavaScript method callback and its managed callback reference.
/// </summary>
public sealed class DomCallbackRegistration : IAsyncDisposable
{
    private readonly IDomRuntime _runtime;
    private readonly int _registrationId;
    private readonly DotNetObjectReference<DomCallbackHandler> _handlerReference;
    private int _disposed;

    internal DomCallbackRegistration(
        IDomRuntime runtime,
        int registrationId,
        DotNetObjectReference<DomCallbackHandler> handlerReference)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _registrationId = registrationId;
        _handlerReference = handlerReference
            ?? throw new ArgumentNullException(nameof(handlerReference));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await _runtime.RemoveMethodValueCallbackAsync(_registrationId)
                .ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _handlerReference.Value.Dispose();
            _handlerReference.Dispose();
        }
    }
}
