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
        Register(
            typeof(TProxy),
            (reference, dispatchRuntime, proxyFactory) =>
                factory(reference, dispatchRuntime, proxyFactory));
    }

    /// <inheritdoc />
    public void Register(
        Type contractType,
        Func<IJSObjectReference, IDomRuntime, IDomProxyFactory, IDomProxy> factory)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(factory);
        if (!typeof(IDomProxy).IsAssignableFrom(contractType))
        {
            throw new ArgumentException(
                $"Generated proxy contract '{contractType}' must implement {nameof(IDomProxy)}.",
                nameof(contractType));
        }

        _registry[contractType] =
            reference => factory(reference, runtime, this);
    }

    /// <inheritdoc />
    public TProxy Create<TProxy>(IJSObjectReference reference) where TProxy : class, IDomProxy
        => (TProxy)Create(typeof(TProxy), reference);

    /// <inheritdoc />
    public IDomProxy Create(Type contractType, IJSObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(reference);
        if (_registry.TryGetValue(contractType, out var factory))
        {
            return factory(reference);
        }

        throw new InvalidOperationException(
            $"No proxy factory registered for '{contractType.Name}'. " +
            "Register every generated proxy contract through the generated " +
            "service collection extension before creating live DOM references.");
    }
}
