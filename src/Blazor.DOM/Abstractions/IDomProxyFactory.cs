// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Typed proxy factory that maps native JS references to generated C# proxy
/// implementations without reflection-heavy per-call dispatch.  Register
/// each generated proxy type once at startup via
/// <see cref="Register{TProxy}"/>, then obtain instances via
/// <see cref="Create{TProxy}"/>.
/// </summary>
public interface IDomProxyFactory
{
    /// <summary>
    /// Registers a factory delegate for <typeparamref name="TProxy"/>.
    /// The delegate receives the raw JS reference, the runtime, and this
    /// factory instance so proxy constructors can forward these dependencies.
    /// </summary>
    /// <typeparam name="TProxy">Generated proxy type.</typeparam>
    /// <param name="factory">Factory delegate.</param>
    void Register<TProxy>(
        Func<IJSObjectReference, IDomRuntime, IDomProxyFactory, TProxy> factory)
        where TProxy : class, IDomProxy;

    /// <summary>Registers a generated proxy factory by its public contract type.</summary>
    void Register(
        Type contractType,
        Func<IJSObjectReference, IDomRuntime, IDomProxyFactory, IDomProxy> factory);

    /// <summary>Registers a generated open generic contract and proxy pair.</summary>
    void RegisterOpenGeneric(Type contractType, Type proxyType);

    /// <summary>
    /// Creates a <typeparamref name="TProxy"/> wrapping <paramref name="reference"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no factory has been registered for <typeparamref name="TProxy"/>.
    /// </exception>
    TProxy Create<TProxy>(IJSObjectReference reference) where TProxy : class, IDomProxy;

    /// <summary>Creates a generated proxy for a runtime-known public contract type.</summary>
    IDomProxy Create(Type contractType, IJSObjectReference reference);
}
