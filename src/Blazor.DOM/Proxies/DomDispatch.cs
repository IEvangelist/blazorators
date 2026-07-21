// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Shared dispatch primitives used by generated host-specific interface bodies.
/// </summary>
public static class DomDispatch
{
    /// <summary>Reads a Server/async property using reviewed transport metadata.</summary>
    public static async ValueTask<TResult> GetPropertyAsync<TResult>(
        IDomDispatchProxy proxy,
        string name,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
    {
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
            cancellationToken).ConfigureAwait(false);
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
            cancellationToken).ConfigureAwait(false);
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
            or DomTransportKind.Binary
            or DomTransportKind.JsStream
            or DomTransportKind.Transferable))
        {
            throw new DomTransportException(
                $"TypeScript value '{transport.SourceType}' requires " +
                $"{transport.Kind} transport but the CLR result is not a DOM proxy.");
        }
    }
}
