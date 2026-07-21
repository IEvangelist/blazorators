// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Base class for all generated DOM proxy types.  Holds the underlying JS
/// reference, the async dispatch runtime, and the proxy factory used to
/// create child proxies.  Disposing the proxy disposes its owned JS reference.
/// </summary>
public abstract class DomProxyBase : IDomDispatchProxy
{
    private readonly object _ownedResourcesGate = new();
    private readonly List<IDisposable> _ownedResources = [];
    private int _disposed;

    /// <summary>The underlying JS object reference for this proxy.</summary>
    public IJSObjectReference Reference { get; }

    /// <summary>Async dispatch runtime.</summary>
    protected IDomRuntime Runtime { get; }

    /// <summary>Proxy factory for creating child proxies from returned references.</summary>
    protected IDomProxyFactory Factory { get; }

    /// <inheritdoc />
    IDomRuntime IDomDispatchProxy.DispatchRuntime => Runtime;

    /// <inheritdoc />
    IDomProxyFactory IDomDispatchProxy.DispatchFactory => Factory;

    /// <param name="reference">Owned JS object reference.</param>
    /// <param name="runtime">DOM runtime for dispatch.</param>
    /// <param name="factory">Proxy factory for wrapping child references.</param>
    protected DomProxyBase(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Runtime   = runtime   ?? throw new ArgumentNullException(nameof(runtime));
        Factory   = factory   ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public ValueTask<DomReferenceEventSubscription<TEvent>>
        SubscribeAsync<TEvent>(
            DomEventDescriptor<TEvent> descriptor,
            Func<DomBorrowedReference<TEvent>, Task> callback,
            DomEventListenerOptions? options = null,
            CancellationToken cancellationToken = default)
        where TEvent : class, IDomProxy =>
        Runtime.SubscribeAsync(
            Reference,
            descriptor,
            Factory,
            callback,
            options,
            cancellationToken);

    /// <inheritdoc />
    public ValueTask<DomEventSubscription> SubscribeValueAsync<TValue>(
        DomEventDescriptor<TValue> descriptor,
        Func<TValue, Task> callback,
        DomEventListenerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Runtime.SubscribeValueAsync(
            Reference,
            descriptor,
            callback,
            options,
            cancellationToken);

    /// <summary>
    /// Disposes the underlying JS reference.  Idempotent; safe to call
    /// multiple times or concurrently.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            IDisposable[] resources;
            lock (_ownedResourcesGate)
            {
                resources = [.. _ownedResources];
                _ownedResources.Clear();
            }
            foreach (var resource in resources)
                resource.Dispose();
            try
            {
                await Reference.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Circuit torn down — reference is already invalid.
            }
        }
    }

    internal void AttachOwnedResource(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_ownedResourcesGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                resource.Dispose();
                throw new ObjectDisposedException(GetType().Name);
            }
            _ownedResources.Add(resource);
        }
    }
}
