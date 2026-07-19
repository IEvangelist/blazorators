// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Default <see cref="IDomProxyFactory"/> implementation.  Register generated
/// proxy types at startup via <see cref="Register{TProxy}"/>; the registration
/// closure captures the <see cref="IDomRuntime"/> and this factory so no
/// per-call service-provider lookup is needed.
/// </summary>
/// <inheritdoc cref="IDomProxyFactory"/>
public sealed class DomProxyFactory(IDomRuntime runtime) : IDomProxyFactory
{
    private readonly ConcurrentDictionary<Type, Func<IJSObjectReference, IDomProxy>> _registry = new();

    /// <inheritdoc />
    public void Register<TProxy>(
        Func<IJSObjectReference, IDomRuntime, IDomProxyFactory, TProxy> factory)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registry[typeof(TProxy)] = reference => factory(reference, runtime, this);
    }

    /// <inheritdoc />
    public TProxy Create<TProxy>(IJSObjectReference reference) where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (_registry.TryGetValue(typeof(TProxy), out var factory))
        {
            return (TProxy)factory(reference);
        }

        throw new InvalidOperationException(
            $"No proxy factory registered for '{typeof(TProxy).Name}'. " +
            $"Call Register<{typeof(TProxy).Name}>() on {nameof(IDomProxyFactory)} " +
            $"before calling Create<{typeof(TProxy).Name}>().");
    }
}
