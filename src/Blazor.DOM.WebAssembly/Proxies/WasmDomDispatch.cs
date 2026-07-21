// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>In-process dispatch primitives used by generated WebAssembly interfaces.</summary>
public static class WasmDomDispatch
{
    /// <summary>Constructs and wraps an owned DOM proxy synchronously.</summary>
    public static TResult Construct<TResult>(
        IDomDispatchProxy owner,
        string constructorPath,
        object?[]? arguments)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(constructorPath);
        return (TResult)owner.DispatchFactory.Create(
            typeof(TResult),
            RequireSyncRuntime(owner).ConstructRef(constructorPath, arguments));
    }

    /// <summary>Reads a property synchronously.</summary>
    public static TResult GetProperty<TResult>(
        IDomDispatchProxy proxy,
        string name,
        DomTransportDescriptor transport)
    {
        transport = DomDispatch.ResolveTransport<TResult>(transport);
        DomDispatch.Validate(proxy, name, transport);
        var runtime = RequireSyncRuntime(proxy);
        var reference = RequireSyncReference(proxy);
        if (typeof(IDomProxy).IsAssignableFrom(typeof(TResult)))
        {
            transport.RequireReference(nameof(transport));
            return (TResult)proxy.DispatchFactory.Create(
                typeof(TResult),
                runtime.GetPropertyRef(reference, name));
        }

        DomDispatch.RequireJsonLike(transport);
        return runtime.GetProperty<TResult>(
            reference,
            name,
            allowStructuredClone:
                transport.Kind == DomTransportKind.StructuredClone);
    }

    /// <summary>Writes a property synchronously.</summary>
    public static void SetProperty<TValue>(
        IDomDispatchProxy proxy,
        string name,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireSyncRuntime(proxy).SetProperty(
            RequireSyncReference(proxy),
            name,
            value);
    }

    /// <summary>Invokes a non-Promise method synchronously.</summary>
    public static TResult Invoke<TResult>(
        IDomDispatchProxy proxy,
        string name,
        object?[]? arguments,
        DomTransportDescriptor transport)
    {
        transport = DomDispatch.ResolveTransport<TResult>(transport);
        DomDispatch.Validate(proxy, name, transport);
        var runtime = RequireSyncRuntime(proxy);
        var reference = RequireSyncReference(proxy);
        if (typeof(IDomProxy).IsAssignableFrom(typeof(TResult)))
        {
            transport.RequireReference(nameof(transport));
            return (TResult)proxy.DispatchFactory.Create(
                typeof(TResult),
                runtime.InvokeMethodRef(reference, name, arguments));
        }

        DomDispatch.RequireJsonLike(transport);
        return runtime.InvokeMethod<TResult>(
            reference,
            name,
            arguments,
            allowStructuredClone:
                transport.Kind == DomTransportKind.StructuredClone);
    }

    /// <summary>Invokes a non-Promise void method synchronously.</summary>
    public static void InvokeVoid(
        IDomDispatchProxy proxy,
        string name,
        object?[]? arguments)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireSyncRuntime(proxy).InvokeMethodVoid(
            RequireSyncReference(proxy),
            name,
            arguments);
    }

    /// <summary>Reads an indexed value synchronously.</summary>
    public static TResult GetIndex<TResult>(
        IDomDispatchProxy proxy,
        object key,
        DomTransportDescriptor transport)
    {
        DomDispatch.Validate(proxy, "index", transport);
        var runtime = RequireSyncRuntime(proxy);
        var reference = RequireSyncReference(proxy);
        if (typeof(IDomProxy).IsAssignableFrom(typeof(TResult)))
        {
            transport.RequireReference(nameof(transport));
            return (TResult)proxy.DispatchFactory.Create(
                typeof(TResult),
                runtime.GetIndexRef(reference, key));
        }

        DomDispatch.RequireJsonLike(transport);
        return runtime.GetIndex<TResult>(reference, key);
    }

    /// <summary>Writes an indexed value synchronously.</summary>
    public static void SetIndex<TValue>(
        IDomDispatchProxy proxy,
        object key,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(key);
        RequireSyncRuntime(proxy).SetIndex(
            RequireSyncReference(proxy),
            key,
            value);
    }

    private static IDomSyncRuntime RequireSyncRuntime(IDomDispatchProxy proxy) =>
        proxy.DispatchRuntime as IDomSyncRuntime
        ?? throw new InvalidOperationException(
            "Synchronous DOM dispatch requires the Blazor WebAssembly package.");

    private static IJSInProcessObjectReference RequireSyncReference(
        IDomDispatchProxy proxy) =>
        proxy.Reference as IJSInProcessObjectReference
        ?? throw new InvalidOperationException(
            "Synchronous DOM dispatch requires an in-process JS object reference.");
}
