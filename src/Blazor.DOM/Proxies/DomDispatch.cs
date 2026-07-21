// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Shared dispatch primitives used by generated host-specific interface bodies.
/// </summary>
public static class DomDispatch
{
    /// <summary>Infers transport for a standard structural container result.</summary>
    public static DomTransportDescriptor InferTransport<TResult>(string sourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        return typeof(IDomProxy).IsAssignableFrom(typeof(TResult))
            ? DomTransportDescriptor.JsReference(sourceType)
            : DomTransportDescriptor.JsonValue(sourceType);
    }

    /// <summary>Constructs and wraps an owned DOM proxy asynchronously.</summary>
    public static async ValueTask<TResult> ConstructAsync<TResult>(
        IDomDispatchProxy owner,
        string constructorPath,
        object?[]? arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(constructorPath);
        var reference = await owner.DispatchRuntime.ConstructAsync(
            constructorPath,
            arguments,
            cancellationToken).ConfigureAwait(false);
        return CreateProxy<TResult>(owner, reference);
    }

    /// <summary>
    /// Constructs and wraps an owned proxy whose persistent callback receives
    /// two callback-scoped typed references.
    /// </summary>
    public static async ValueTask<TResult>
        ConstructReferencePairCallbackAsync<TResult, TFirst, TSecond>(
            IDomDispatchProxy owner,
            string constructorPath,
            int callbackArgumentIndex,
            object?[]? arguments,
            DomTransportDescriptor firstTransport,
            DomTransportDescriptor secondTransport,
            Func<
                DomBorrowedReference<TFirst>,
                DomBorrowedReference<TSecond>,
                Task> callback,
            CancellationToken cancellationToken = default)
        where TResult : class, IDomProxy
        where TFirst : class, IDomProxy
        where TSecond : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(constructorPath);
        ArgumentNullException.ThrowIfNull(callback);
        using var construction = await owner.DispatchRuntime
            .ConstructReferencePairCallbackAsync(
                constructorPath,
                callbackArgumentIndex,
                arguments,
                owner.DispatchFactory,
                firstTransport,
                secondTransport,
                callback,
                cancellationToken)
            .ConfigureAwait(false);
        var proxy = CreateProxy<TResult>(owner, construction.Reference);
        if (proxy is not DomProxyBase resourceOwner)
        {
            await proxy.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Callback-backed proxy '{typeof(TResult)}' must derive from " +
                $"{nameof(DomProxyBase)}.");
        }
        resourceOwner.AttachOwnedResource(construction.TakeCallbackResource());
        return proxy;
    }

    /// <summary>Reads a Server/async property using reviewed transport metadata.</summary>
    public static async ValueTask<TResult> GetPropertyAsync<TResult>(
        IDomDispatchProxy proxy,
        string name,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
    {
        transport = ResolveTransport<TResult>(transport);
        Validate(proxy, name, transport);
        if (IsProxyContract<TResult>())
        {
            transport.RequireReference(nameof(transport));
            var reference = await proxy.DispatchRuntime.GetPropertyRefAsync(
                proxy.Reference,
                name,
                cancellationToken).ConfigureAwait(false);
            return CreateProxy<TResult>(proxy, reference);
        }

        RequireJsonLike(transport);
        return await proxy.DispatchRuntime.GetPropertyAsync<TResult>(
            proxy.Reference,
            name,
            cancellationToken,
            allowStructuredClone:
                transport.Kind == DomTransportKind.StructuredClone).ConfigureAwait(false);
    }

    /// <summary>Writes a Server/async property.</summary>
    public static ValueTask SetPropertyAsync<TValue>(
        IDomDispatchProxy proxy,
        string name,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return proxy.DispatchRuntime.SetPropertyAsync(
            proxy.Reference,
            name,
            value,
            cancellationToken);
    }

    /// <summary>Invokes a Server/async method using reviewed result transport.</summary>
    public static async ValueTask<TResult> InvokeAsync<TResult>(
        IDomDispatchProxy proxy,
        string name,
        object?[]? arguments,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
    {
        transport = ResolveTransport<TResult>(transport);
        Validate(proxy, name, transport);
        if (IsProxyContract<TResult>())
        {
            transport.RequireReference(nameof(transport));
            var reference = await proxy.DispatchRuntime.InvokeMethodRefAsync(
                proxy.Reference,
                name,
                arguments,
                cancellationToken).ConfigureAwait(false);
            return CreateProxy<TResult>(proxy, reference);
        }

        RequireJsonLike(transport);
        return await proxy.DispatchRuntime.InvokeMethodAsync<TResult>(
            proxy.Reference,
            name,
            arguments,
            cancellationToken,
            allowStructuredClone:
                transport.Kind == DomTransportKind.StructuredClone).ConfigureAwait(false);
    }

    /// <summary>Invokes a Server/async void method.</summary>
    public static ValueTask InvokeVoidAsync(
        IDomDispatchProxy proxy,
        string name,
        object?[]? arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return proxy.DispatchRuntime.InvokeMethodVoidAsync(
            proxy.Reference,
            name,
            arguments,
            cancellationToken);
    }

    /// <summary>
    /// Invokes a Promise-returning method with a one-shot typed-reference callback
    /// whose managed result becomes the operation result.
    /// </summary>
    public static ValueTask<TResult> InvokeReferenceResultCallbackAsync<TProxy, TResult>(
        IDomDispatchProxy proxy,
        string name,
        int callbackArgumentIndex,
        object?[]? arguments,
        DomTransportDescriptor callbackTransport,
        Func<DomBorrowedReference<TProxy>?, Task<TResult>> callback,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy
    {
        Validate(proxy, name, callbackTransport);
        ArgumentNullException.ThrowIfNull(callback);
        return proxy.DispatchRuntime
            .InvokeMethodReferenceResultCallbackAsync(
                proxy.Reference,
                name,
                callbackArgumentIndex,
                arguments,
                proxy.DispatchFactory,
                callbackTransport,
                callback,
                cancellationToken);
    }

    /// <summary>Reads an indexed value asynchronously.</summary>
    public static async ValueTask<TResult> GetIndexAsync<TResult>(
        IDomDispatchProxy proxy,
        object key,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
    {
        Validate(proxy, "index", transport);
        if (IsProxyContract<TResult>())
        {
            transport.RequireReference(nameof(transport));
            var reference = await proxy.DispatchRuntime.GetIndexRefAsync(
                proxy.Reference,
                key,
                cancellationToken).ConfigureAwait(false);
            return CreateProxy<TResult>(proxy, reference);
        }

        RequireJsonLike(transport);
        return await proxy.DispatchRuntime.GetIndexAsync<TResult>(
            proxy.Reference,
            key,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes an indexed value asynchronously.</summary>
    public static ValueTask SetIndexAsync<TValue>(
        IDomDispatchProxy proxy,
        object key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(key);
        return proxy.DispatchRuntime.SetIndexAsync(
            proxy.Reference,
            key,
            value,
            cancellationToken);
    }

    /// <summary>Combines fixed arguments and a TypeScript rest argument.</summary>
    public static object?[] CombineArguments(object?[] fixedArguments, Array rest)
    {
        ArgumentNullException.ThrowIfNull(fixedArguments);
        ArgumentNullException.ThrowIfNull(rest);
        var result = new object?[fixedArguments.Length + rest.Length];
        fixedArguments.CopyTo(result, 0);
        for (var index = 0; index < rest.Length; index++)
            result[fixedArguments.Length + index] = rest.GetValue(index);
        return result;
    }

    private static bool IsProxyContract<TResult>() =>
        typeof(IDomProxy).IsAssignableFrom(typeof(TResult));

    private static TResult CreateProxy<TResult>(
        IDomDispatchProxy owner,
        IJSObjectReference reference) =>
        (TResult)owner.DispatchFactory.Create(typeof(TResult), reference);

    internal static void Validate(
        IDomDispatchProxy proxy,
        string name,
        DomTransportDescriptor transport)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        if (transport.Kind == DomTransportKind.Unsupported)
        {
            throw new DomTransportException(
                $"TypeScript value '{transport.SourceType}' is unsupported: " +
                transport.Reason);
        }
    }

    internal static void RequireJsonLike(DomTransportDescriptor transport)
    {
        if (transport.Kind is not (
            DomTransportKind.JsonValue
            or DomTransportKind.StructuredClone
            or DomTransportKind.Binary
            or DomTransportKind.JsStream
            or DomTransportKind.Transferable))
        {
            throw new DomTransportException(
                $"TypeScript value '{transport.SourceType}' requires " +
                $"{transport.Kind} transport but the CLR result is not a DOM proxy.");
        }
    }

    internal static DomTransportDescriptor ResolveTransport<TResult>(
        DomTransportDescriptor transport)
    {
        if (transport.Kind != DomTransportKind.Inferred)
            return transport;
        if (IsProxyContract<TResult>())
        {
            return DomTransportDescriptor.JsReference(
                transport.SourceType,
                transport.Nullable);
        }
        return typeof(TResult) == typeof(object)
            ? DomTransportDescriptor.StructuredCloneValue(
                transport.SourceType,
                transport.Nullable)
            : DomTransportDescriptor.JsonValue(
                transport.SourceType,
                transport.Nullable);
    }
}
