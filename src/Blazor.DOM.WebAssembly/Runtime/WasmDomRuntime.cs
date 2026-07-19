// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Blazor WebAssembly implementation of <see cref="IDomSyncRuntime"/>.
/// Uses <see cref="IJSRuntime"/> for the async module import (which returns
/// an <see cref="IJSInProcessObjectReference"/> on WASM) and exposes
/// synchronous dispatch paths once the module has been initialised.
/// The module is imported using the same single-flight mechanism as
/// <see cref="ServerDomRuntime"/>: concurrent callers share the in-progress
/// import, failed or cancelled attempts are cleared for retry, and caller
/// cancellation never poisons the shared task.
/// </summary>
internal sealed class WasmDomRuntime : IDomSyncRuntime, IAsyncDisposable
{
    internal const string ModulePath = "./_content/Blazor.DOM/blazorators.dom.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private Task<IJSInProcessObjectReference>? _importTask;
    private int _disposed;

    public WasmDomRuntime(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _jsRuntime = jsRuntime;
    }

    // ── Module access ──────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the module is imported.  Must be awaited before any sync call.
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);

    private IJSInProcessObjectReference GetSyncModule()
    {
        var task = Volatile.Read(ref _importTask);
        if (task is not { IsCompletedSuccessfully: true })
        {
            throw new InvalidOperationException(
                "The Blazor DOM WASM module has not been initialised. " +
                "Await an async DOM call (e.g. GetWindowAsync) or " +
                "WasmDomRuntime.InitializeAsync() before using synchronous members.");
        }
        return task.Result;
    }

    private async ValueTask<IJSInProcessObjectReference> GetAsyncModuleAsync(CancellationToken ct)
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
            // Re-check disposed while holding the lock.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            task = _importTask;
            if (task is { IsCompletedSuccessfully: true })
                return task.Result;

            // Faulted or cancelled import (or no prior attempt): start fresh.
            if (task is null || task.IsFaulted || task.IsCanceled)
                _importTask = task = _jsRuntime
                    .InvokeAsync<IJSInProcessObjectReference>("import", ModulePath).AsTask();
            // else: import is in progress — share the running task.

            try
            {
                var module = await task.WaitAsync(ct).ConfigureAwait(false);

                // DisposeAsync/Dispose may have set _disposed while we awaited.
                // Dispose the newly-imported module (before releasing the lock so
                // Dispose/DisposeAsync sees _importTask=null and skips a second disposal).
                if (Volatile.Read(ref _disposed) != 0)
                {
                    _importTask = null;
                    try { if (module is IDisposable d) d.Dispose(); }
                    catch (JSDisconnectedException) { }
                    throw new ObjectDisposedException(nameof(WasmDomRuntime));
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

    private void ClearIfFailed(Task<IJSInProcessObjectReference> task)
    {
        if (task.IsFaulted || task.IsCanceled)
            _importTask = null;
    }

    // ── IDomSyncRuntime ────────────────────────────────────────────────────

    /// <inheritdoc />
    public TValue GetProperty<TValue>(IJSInProcessObjectReference reference, string name) =>
        GetSyncModule().Invoke<TValue>("getProperty", reference, name);

    /// <inheritdoc />
    public void SetProperty(IJSInProcessObjectReference reference, string name, object? value) =>
        GetSyncModule().InvokeVoid("setProperty", reference, name, value);

    /// <inheritdoc />
    public TResult InvokeMethod<TResult>(
        IJSInProcessObjectReference reference, string name, object?[]? args) =>
        GetSyncModule().Invoke<TResult>("invokeMethod", reference, name, DomArguments.Unwrap(args));

    /// <inheritdoc />
    public void InvokeMethodVoid(
        IJSInProcessObjectReference reference, string name, object?[]? args) =>
        GetSyncModule().InvokeVoid("invokeMethod", reference, name, DomArguments.Unwrap(args));

    /// <inheritdoc />
    public IJSInProcessObjectReference InvokeMethodRef(
        IJSInProcessObjectReference reference, string name, object?[]? args) =>
        GetSyncModule().Invoke<IJSInProcessObjectReference>(
            "invokeMethod", reference, name, DomArguments.Unwrap(args));

    /// <inheritdoc />
    public TValue GetIndex<TValue>(IJSInProcessObjectReference reference, int index) =>
        GetSyncModule().Invoke<TValue>("getIndex", reference, index);

    /// <inheritdoc />
    public void SetIndex(IJSInProcessObjectReference reference, int index, object? value) =>
        GetSyncModule().InvokeVoid("setIndex", reference, index, value);

    // ── IDomRuntime (async paths) ──────────────────────────────────────────

    /// <inheritdoc />
    public async ValueTask<TValue> GetPropertyAsync<TValue>(
        IJSObjectReference reference, string name, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getProperty", cancellationToken, [reference, name]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetPropertyAsync(
        IJSObjectReference reference, string name, object? value, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setProperty", cancellationToken, [reference, name, value]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TResult> InvokeMethodAsync<TResult>(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TResult>(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeMethodVoidAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> InvokeMethodRefAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "invokeMethod", cancellationToken, [reference, name, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetGlobalAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "getGlobal", cancellationToken, [path]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> ConstructAsync(
        string ctorPath, object?[]? args, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "construct", cancellationToken, [ctorPath, DomArguments.Unwrap(args)])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference, int index, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getIndex", cancellationToken, [reference, index]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetIndexAsync(
        IJSObjectReference reference, int index, object? value, CancellationToken cancellationToken = default)
    {
        var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setIndex", cancellationToken, [reference, index, value]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomEventSubscription> AddEventListenerAsync(
        IJSObjectReference target, string type, Func<string, Task> callback,
        CancellationToken cancellationToken = default)
    {
        var handler    = new DomCallbackHandler(callback);
        var handlerRef = DotNetObjectReference.Create(handler);
        try
        {
            var module = await GetAsyncModuleAsync(cancellationToken).ConfigureAwait(false);
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
    public async ValueTask RemoveEventListenerAsync(int listenerId, CancellationToken cancellationToken = default)
    {
        // Only attempt removal when the module was successfully imported.
        var task = Volatile.Read(ref _importTask);
        if (task is not { IsCompletedSuccessfully: true }) return;

        await task.Result.InvokeVoidAsync(
            "removeDotNetEventListener", cancellationToken, [listenerId]).ConfigureAwait(false);
    }

    // ── IAsyncDisposable ───────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Acquire lock to synchronize with any in-progress GetAsyncModuleAsync.
        await _importLock.WaitAsync().ConfigureAwait(false);
        Task<IJSInProcessObjectReference>? importTask;
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
        IJSInProcessObjectReference? module;
        try
        {
            module = await importTask.ConfigureAwait(false);
        }
        catch
        {
            // Import faulted or was cancelled — no module was produced; nothing to free.
            return;
        }

        // IJSInProcessObjectReference is synchronously disposable on WASM.
        try
        {
            if (module is IDisposable syncRef)
                syncRef.Dispose();
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        // Unexpected errors propagate.
    }
}
