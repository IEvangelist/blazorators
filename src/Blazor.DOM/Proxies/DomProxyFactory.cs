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
    private readonly ConcurrentDictionary<Type, Type> _openGenericRegistry = new();

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
    public void RegisterOpenGeneric(Type contractType, Type proxyType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(proxyType);
        if (!contractType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"Contract '{contractType}' must be an open generic type.",
                nameof(contractType));
        }
        if (!proxyType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"Proxy '{proxyType}' must be an open generic type.",
                nameof(proxyType));
        }
        if (contractType.GetGenericArguments().Length
            != proxyType.GetGenericArguments().Length)
        {
            throw new ArgumentException(
                "Open generic contract and proxy arity must match.",
                nameof(proxyType));
        }
        _openGenericRegistry[contractType] = proxyType;
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
        if (contractType.IsConstructedGenericType
            && _openGenericRegistry.TryGetValue(
                contractType.GetGenericTypeDefinition(),
                out var proxyDefinition))
        {
            var proxyType = proxyDefinition.MakeGenericType(
                contractType.GetGenericArguments());
            return (IDomProxy)(Activator.CreateInstance(
                proxyType,
                reference,
                runtime,
                this)
                ?? throw new InvalidOperationException(
                    $"Generated proxy '{proxyType}' could not be constructed."));
        }
        if (contractType.IsConstructedGenericType
            && contractType.GetGenericTypeDefinition() is var arrayContract
            && (arrayContract == typeof(IReadOnlyBrowserArray<>)
                || arrayContract == typeof(IBrowserArray<>)))
        {
            var proxyType = typeof(BrowserArrayDomProxy<>).MakeGenericType(
                contractType.GetGenericArguments());
            return (IDomProxy)(Activator.CreateInstance(
                proxyType,
                reference,
                runtime,
                this)
                ?? throw new InvalidOperationException(
                    $"Browser array proxy '{proxyType}' could not be constructed."));
        }

        throw new InvalidOperationException(
            $"No proxy factory registered for '{contractType.Name}'. " +
            "Register every generated proxy contract through the generated " +
            "service collection extension before creating live DOM references.");
    }
}
