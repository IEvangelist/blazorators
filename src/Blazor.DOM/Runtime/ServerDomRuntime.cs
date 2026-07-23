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
    internal static readonly string ModulePath =
        DomModulePath.ForAssemblyContaining<ServerDomRuntime>();

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
        IJSObjectReference reference,
        string name,
        CancellationToken cancellationToken = default,
        bool allowStructuredClone = false)
    {
        DomTransportValidator.ValidateJsonResult<TValue>(allowStructuredClone);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getProperty", cancellationToken, [reference, name]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetPropertyRefAsync(
        IJSObjectReference reference,
        string name,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.GetPropertyObjectReferenceAsync(
            module,
            reference,
            name,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TProxy?> GetPropertyReferenceAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.GetPropertyReferenceAsync<TProxy>(
            module,
            proxyFactory,
            reference,
            name,
            transport,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetPropertyAsync(
        IJSObjectReference reference, string name, object? value, CancellationToken cancellationToken = default)
    {
        var preparedValue = DomArguments.PrepareValue(value, $"property '{name}'");
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setProperty", cancellationToken, [reference, name, preparedValue]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TResult> InvokeMethodAsync<TResult>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        CancellationToken cancellationToken = default,
        bool allowStructuredClone = false)
    {
        DomTransportValidator.ValidateJsonResult<TResult>(allowStructuredClone);
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TResult>(
            "invokeMethod", cancellationToken, [reference, name, preparedArgs])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TResult> InvokeMethodUnionAsync<TResult>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        IReadOnlyList<DomUnionInboundArm<TResult>> arms,
        CancellationToken cancellationToken = default)
    {
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.InvokeMethodUnionAsync(
            module,
            reference,
            name,
            preparedArgs,
            arms,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeMethodVoidAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "invokeMethod", cancellationToken, [reference, name, preparedArgs])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> InvokeMethodRefAsync(
        IJSObjectReference reference, string name, object?[]? args, CancellationToken cancellationToken = default)
    {
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.InvokeMethodObjectReferenceAsync(
            module,
            reference,
            name,
            preparedArgs,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TProxy?> InvokeMethodReferenceAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.InvokeMethodReferenceAsync<TProxy>(
            module,
            proxyFactory,
            reference,
            name,
            preparedArgs,
            transport,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeMethodReferenceCallbackAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        int callbackArgumentIndex,
        object?[]? args,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task> callback,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        ArgumentNullException.ThrowIfNull(callback);
        var preparedArgs = DomArguments.Prepare(args);
        var argumentCount = preparedArgs?.Length ?? 0;
        if (callbackArgumentIndex < 0 || callbackArgumentIndex > argumentCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callbackArgumentIndex),
                callbackArgumentIndex,
                "The callback index must identify an insertion point in the argument list.");
        }
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await DomRuntimeTransport.InvokeReferenceCallbackAsync(
            module,
            proxyFactory,
            reference,
            name,
            callbackArgumentIndex,
            preparedArgs,
            transport,
            callback,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TResult>
        InvokeMethodReferenceResultCallbackAsync<TProxy, TResult>(
            IJSObjectReference reference,
            string name,
            int callbackArgumentIndex,
            object?[]? args,
            IDomProxyFactory proxyFactory,
            DomTransportDescriptor transport,
            Func<DomBorrowedReference<TProxy>?, Task<TResult>> callback,
            CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        ArgumentNullException.ThrowIfNull(callback);
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport
            .InvokeReferenceResultCallbackAsync(
                module,
                proxyFactory,
                reference,
                name,
                callbackArgumentIndex,
                preparedArgs,
                transport,
                callback,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomReadStream> InvokeMethodStreamAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireStreamable(nameof(transport));
        if (transport.Nullable)
        {
            throw new ArgumentException(
                "Nullable stream results must use InvokeMethodNullableStreamAsync.",
                nameof(transport));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.InvokeMethodStreamAsync(
            module,
            reference,
            name,
            preparedArgs,
            transport,
            maximumLength,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomReadStream?> InvokeMethodNullableStreamAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        if (!transport.Nullable)
        {
            throw new ArgumentException(
                "Nullable stream results require nullable transport metadata.",
                nameof(transport));
        }
        transport.RequireStreamable(nameof(transport));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.InvokeMethodNullableStreamAsync(
            module,
            reference,
            name,
            preparedArgs,
            transport,
            maximumLength,
            cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<bool> IsGlobalAvailableAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<bool>(
            "hasGlobal",
            cancellationToken,
            [path]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> ConstructAsync(
        string ctorPath, object?[]? args, CancellationToken cancellationToken = default)
    {
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>(
            "construct", cancellationToken, [ctorPath, preparedArgs])
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomCallbackConstruction>
        ConstructReferencePairCallbackAsync<TFirst, TSecond>(
            string ctorPath,
            int callbackArgumentIndex,
            object?[]? args,
            IDomProxyFactory proxyFactory,
            DomTransportDescriptor firstTransport,
            DomTransportDescriptor secondTransport,
            Func<
                DomBorrowedReference<TFirst>,
                DomBorrowedReference<TSecond>,
                Task> callback,
            CancellationToken cancellationToken = default)
        where TFirst : class, IDomProxy
        where TSecond : class, IDomProxy
    {
        var preparedArgs = DomArguments.Prepare(args);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.ConstructReferencePairCallbackAsync(
            module,
            proxyFactory,
            ctorPath,
            callbackArgumentIndex,
            preparedArgs,
            firstTransport,
            secondTransport,
            callback,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference, int index, CancellationToken cancellationToken = default)
        => await GetIndexAsync<TValue>(
            reference,
            (object)index,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference, object index, CancellationToken cancellationToken = default)
    {
        DomTransportValidator.ValidateJsonResult<TValue>();
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<TValue>(
            "getIndex", cancellationToken, [reference, index]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetIndexRefAsync(
        IJSObjectReference reference,
        int index,
        CancellationToken cancellationToken = default)
        => await GetIndexRefAsync(
            reference,
            (object)index,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetIndexRefAsync(
        IJSObjectReference reference,
        object index,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.GetIndexObjectReferenceAsync(
            module,
            reference,
            index,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TProxy?> GetIndexReferenceAsync<TProxy>(
        IJSObjectReference reference,
        int index,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.GetIndexReferenceAsync<TProxy>(
            module,
            proxyFactory,
            reference,
            index,
            transport,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetIndexAsync(
        IJSObjectReference reference, int index, object? value, CancellationToken cancellationToken = default)
        => await SetIndexAsync(
            reference,
            (object)index,
            value,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask SetIndexAsync(
        IJSObjectReference reference, object index, object? value, CancellationToken cancellationToken = default)
    {
        var preparedValue = DomArguments.PrepareValue(value, $"index [{index}]");
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync(
            "setIndex", cancellationToken, [reference, index, preparedValue]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomReadStream> OpenReadStreamAsync(
        IJSObjectReference reference,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireStreamable(nameof(transport));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.OpenReadStreamAsync(
            module,
            reference,
            transport,
            maximumLength,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomEventSubscription> AddEventListenerAsync(
        IJSObjectReference target,
        string type,
        Func<string, Task> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(callback);
        var handler = new DomCallbackHandler(callback);
        var handlerRef = DotNetObjectReference.Create(handler);
        try
        {
            var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
            var listenerId = await DomRuntimeTransport.AddEventListenerAsync(
                module,
                target,
                type,
                handlerRef,
                null,
                cancellationToken).ConfigureAwait(false);

            return new DomEventSubscription(this, listenerId, handlerRef);
        }

        catch
        {
            handler.Dispose();
            handlerRef.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<DomEventSubscription> SubscribeValueAsync<TValue>(
        IJSObjectReference target,
        DomEventDescriptor<TValue> descriptor,
        Func<TValue, Task> callback,
        DomEventListenerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(callback);
        if (descriptor.Transport.Kind != DomTransportKind.JsonValue)
        {
            throw new ArgumentException(
                "Value subscriptions require a JSON-valued descriptor.",
                nameof(descriptor));
        }
        var handler = new DomCallbackHandler(json =>
        {
            var value = System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
            return callback(value!);
        });
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
            var listenerId = await DomRuntimeTransport.AddEventListenerAsync(
                module,
                target,
                descriptor.Name,
                handlerReference,
                options,
                cancellationToken).ConfigureAwait(false);
            return new DomEventSubscription(this, listenerId, handlerReference);
        }
        catch
        {
            handler.Dispose();
            handlerReference.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<DomReferenceEventSubscription<TProxy>>
        AddReferenceEventListenerAsync<TProxy>(
            IJSObjectReference target,
            string type,
            IDomProxyFactory proxyFactory,
            DomTransportDescriptor transport,
            Func<DomBorrowedReference<TProxy>, Task> callback,
            CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        if (transport.Nullable)
        {
            throw new ArgumentException(
                "DOM event references are not nullable.",
                nameof(transport));
        }
        ArgumentNullException.ThrowIfNull(callback);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.AddReferenceEventListenerAsync(
            module,
            proxyFactory,
            target,
            type,
            transport,
            null,
            callback,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DomReferenceEventSubscription<TEvent>>
        SubscribeAsync<TEvent>(
            IJSObjectReference target,
            DomEventDescriptor<TEvent> descriptor,
            IDomProxyFactory proxyFactory,
            Func<DomBorrowedReference<TEvent>, Task> callback,
            DomEventListenerOptions? options = null,
            CancellationToken cancellationToken = default)
        where TEvent : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await DomRuntimeTransport.AddReferenceEventListenerAsync(
            module,
            proxyFactory,
            target,
            descriptor.Name,
            descriptor.Transport,
            options,
            callback,
            cancellationToken).ConfigureAwait(false);
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
