// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Internal JS-invokable callback holder.  An instance is wrapped in a
/// <see cref="DotNetObjectReference{DomCallbackHandler}"/>, passed to JS, and
/// called back for each event.  Dispose to suppress further invocations before
/// the JS listener is removed.
/// </summary>
internal sealed class DomCallbackHandler : IDisposable
{
    private readonly Func<string, Task> _callback;
    private int _disposed;

    /// <param name="callback">
    /// Delegate to invoke with the serialised event JSON on each event
    /// notification from JS.
    /// </param>
    public DomCallbackHandler(Func<string, Task> callback)
        => _callback = callback ?? throw new ArgumentNullException(nameof(callback));

    /// <summary>
    /// Called by JS via dotnet reference.  Forwards the serialised event JSON
    /// to the registered C# delegate.
    /// </summary>
    [JSInvokable("HandleEvent")]
    public Task HandleEventAsync(string eventJson)
    {
        if (_disposed != 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            return _callback(eventJson);
        }
        catch (Exception ex)
        {
            // Surface to caller; don't swallow.
            return Task.FromException(ex);
        }
    }

    /// <summary>Marks this handler as disposed so future calls are no-ops.</summary>
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
