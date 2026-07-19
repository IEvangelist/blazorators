// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Blazor Server implementation of <see cref="IDomRuntime"/>.  Imports
/// the shared DOM JS module on the first operation using a single-flight
/// mechanism: concurrent callers share the in-progress import task,
/// failed or cancelled attempts are cleared so the next caller retries,
/// and caller cancellation never poisons the shared task.
/// Disposal releases the module reference exactly once.
/// </summary>
internal sealed class ServerDomRuntime : IDomRuntime, IAsyncDisposable
{
    internal const string ModulePath = "./_content/Blazor.DOM/blazorators.dom.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private Task<IJSObjectReference>? _importTask;
    private int _disposed;

    public ServerDomRuntime(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _jsRuntime = jsRuntime;
    }

    // ── Internal module access ─────────────────────────────────────────────

    internal async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken ct)
    {
        // Fast path: already imported successfully.
        var task = Volatile.Read(ref _importTask);
        if (task is { IsCompletedSuccessfully: true })
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ct.ThrowIfCancellationRequested();
            return task.Result;
        }

        // Slow path: coordinate concurrent first-import or retry after failure.
        await _importLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check disposed while holding the lock — DisposeAsync sets _disposed
            // before waiting for the lock, so this is always consistent.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            task = _importTask;
            if (task is { IsCompletedSuccessfully: true })
                return task.Result;

            // Faulted or cancelled import (or no prior attempt): start fresh.
            if (task is null || task.IsFaulted || task.IsCanceled)
                _importTask = task = _jsRuntime
                    .InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            // else: import is in progress — share the running task.

            try
            {
                var module = await task.WaitAsync(ct).ConfigureAwait(false);

                // DisposeAsync may have set _disposed while we were awaiting.
                // Dispose the newly-imported module now (before releasing the lock
                // so DisposeAsync sees _importTask=null and skips a second disposal).
                if (Volatile.Read(ref _disposed) != 0)
                {
                    _importTask = null;
                    try { await module.DisposeAsync().ConfigureAwait(false); }
                    catch (JSDisconnectedException) { }
                    catch (OperationCanceledException) { }
                    throw new ObjectDisposedException(nameof(ServerDomRuntime));
                }
                return module;
            }
            catch (ObjectDisposedException) { throw; }
            catch (JSException ex) when (DomPreRenderingDetection.IsPreRendering(ex))
            {
                ClearIfFailed(task);
                throw DomJSException.Prerendering();
            }
            catch (InvalidOperationException ex) when (DomPreRenderingDetection.IsPreRendering(ex))
            {
                ClearIfFailed(task);
                throw DomJSException.Prerendering();
            }
            catch
            {
                ClearIfFailed(task);
                throw;
            }
        }
        finally
        {
            _importLock.Release();
        }
    }

    private void ClearIfFailed(Task<IJSObjectReference> task)
    {
        if (task.IsFaulted || task.IsCanceled)
            _importTask = null;
    }

    // ── IDomRuntime ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async ValueTask<TValue> GetPropertyAsync<TValue>(
        IJSObjectReference reference, string name, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getProperty", cancellationToken, [reference, name]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetPropertyAsync(
        IJSObjectReference reference, string name, object? value, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setProperty", cancellationToken, [reference, name, value]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TResult> InvokeMethodAsync<TResult>(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TResult>(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeMethodVoidAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> InvokeMethodRefAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetGlobalAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "getGlobal", cancellationToken, [path]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> ConstructAsync(
        string ctorPath, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "construct", cancellationToken, [ctorPath, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference, int index, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getIndex", cancellationToken, [reference, index]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetIndexAsync(
        IJSObjectReference reference, int index, object? value, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setIndex", cancellationToken, [reference, index, value]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomEventSubscription> AddEventListenerAsync(
        IJSObjectReference target,
        string type,
        Func<string, Task> callback,
        CancellationToken cancellationToken = default)
    {
        var handler    = new DomCallbackHandler(callback);
        var handlerRef = DotNetObjectReference.Create(handler);
        try
        {
            var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
            var listenerId = await module.InvokeAsync<int>(
                "addDotNetEventListener", cancellationToken,
                [target, type, handlerRef, "HandleEvent"]).ConfigureAwait(false);

            return new DomEventSubscription(this, listenerId, handlerRef);
        }
        catch
        {
            // Dispose the GC-root reference on every failure path so it is not
            // permanently held in the DotNet object reference table.
            handlerRef.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveEventListenerAsync(
        int listenerId, CancellationToken cancellationToken = default)
    {
        // Only attempt removal when the module was successfully imported;
        // if it never loaded there is nothing to remove on the JS side.
        var task = Volatile.Read(ref _importTask);
        if (task is not { IsCompletedSuccessfully: true }) return;

        await task.Result.InvokeVoidAsync(
            "removeDotNetEventListener", cancellationToken, [listenerId]).ConfigureAwait(false);
    }

    // ── IAsyncDisposable ───────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Acquire lock to synchronize with any in-progress GetModuleAsync:
        // either GetModuleAsync holds the lock and will detect _disposed before
        // it returns (disposing the module itself), or DisposeAsync wins the
        // lock first and captures _importTask before any import can be stored.
        await _importLock.WaitAsync().ConfigureAwait(false);
        Task<IJSObjectReference>? importTask;
        try
        {
            importTask = _importTask;
            _importTask = null;
        }
        finally
        {
            _importLock.Release();
        }

        if (importTask is null) return;

        // Await any in-flight import: a caller's WaitAsync(ct) may have been
        // cancelled while the underlying Task was still running.  Without awaiting
        // here, the module that eventually resolves would leak permanently.
        IJSObjectReference? module;
        try
        {
            module = await importTask.ConfigureAwait(false);
        }
        catch
        {
            // Import faulted or was cancelled — no module was produced; nothing to free.
            return;
        }

        try
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException) { /* circuit already gone */ }
        catch (OperationCanceledException) { /* teardown cancelled */ }
        // Unexpected JS errors from a live-circuit module disposal propagate.
    }
}
