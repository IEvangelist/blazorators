// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Core async dispatch interface for DOM interop operations.  The server
/// runtime implements this with <see cref="IJSObjectReference"/>-backed
/// round-trips; the WASM runtime adds synchronous paths via
/// <c>IDomSyncRuntime</c>.
/// </summary>
public interface IDomRuntime
{
    // ── Property access ──────────────────────────────────────────────────────

    /// <summary>Reads a reviewed JSON-valued property from a live JS object.</summary>
    ValueTask<TValue> GetPropertyAsync<TValue>(
        IJSObjectReference reference,
        string name,
        CancellationToken cancellationToken = default,
        bool allowStructuredClone = false);

    /// <summary>Reads a named property as a live JS object reference.</summary>
    ValueTask<IJSObjectReference> GetPropertyRefAsync(
        IJSObjectReference reference,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a possibly-null named interface property as an owned typed proxy.
    /// </summary>
    ValueTask<TProxy?> GetPropertyReferenceAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>Writes a named property on a live JS object.</summary>
    ValueTask SetPropertyAsync(
        IJSObjectReference reference,
        string name,
        object? value,
        CancellationToken cancellationToken = default);

    // ── Method invocation ────────────────────────────────────────────────────

    /// <summary>
    /// Invokes a method and deserialises the return value as
    /// <typeparamref name="TResult"/> (JSON-safe types).
    /// </summary>
    ValueTask<TResult> InvokeMethodAsync<TResult>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        CancellationToken cancellationToken = default,
        bool allowStructuredClone = false);

    /// <summary>
    /// Invokes a method whose result requires a proven JavaScript-side union
    /// discriminator before JSON or reference transport is selected.
    /// </summary>
    ValueTask<TResult> InvokeMethodUnionAsync<TResult>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        IReadOnlyList<DomUnionInboundArm<TResult>> arms,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes a void method on a live JS object.</summary>
    ValueTask InvokeMethodVoidAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method whose return value is a live JS object and wraps it in
    /// an <see cref="IJSObjectReference"/> for further proxy operations.
    /// </summary>
    ValueTask<IJSObjectReference> InvokeMethodRefAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method whose result is a possibly-null named interface and
    /// returns an owned typed proxy.
    /// </summary>
    ValueTask<TProxy?> InvokeMethodReferenceAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>
    /// Invokes a method with a one-shot callback whose argument is a nullable,
    /// callback-scoped typed JS reference.
    /// </summary>
    ValueTask InvokeMethodReferenceCallbackAsync<TProxy>(
        IJSObjectReference reference,
        string name,
        int callbackArgumentIndex,
        object?[]? args,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task> callback,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>
    /// Invokes a method with a one-shot callback whose nullable typed-reference
    /// argument produces the JSON-valued result of the JavaScript operation.
    /// </summary>
    ValueTask<TResult> InvokeMethodReferenceResultCallbackAsync<TProxy, TResult>(
        IJSObjectReference reference,
        string name,
        int callbackArgumentIndex,
        object?[]? args,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task<TResult>> callback,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>
    /// Invokes a method and opens its Blob, ArrayBuffer, or typed-array result
    /// through an owned, bounded <see cref="IJSStreamReference"/>.
    /// </summary>
    ValueTask<DomReadStream> InvokeMethodStreamAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method whose nullable Blob, ArrayBuffer, or typed-array result
    /// is opened through an owned, bounded <see cref="IJSStreamReference"/>.
    /// A null result is distinct from a non-null, zero-length stream.
    /// </summary>
    ValueTask<DomReadStream?> InvokeMethodNullableStreamAsync(
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default);

    // ── Global access ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a dotted global path (e.g. <c>"document"</c>) and returns a
    /// JS reference to the object.
    /// </summary>
    ValueTask<IJSObjectReference> GetGlobalAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether a dotted global path currently resolves.</summary>
    ValueTask<bool> IsGlobalAvailableAsync(
        string path,
        CancellationToken cancellationToken = default);

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Calls <c>new &lt;ctorPath&gt;(...args)</c> and returns a JS reference
    /// to the constructed object.
    /// </summary>
    ValueTask<IJSObjectReference> ConstructAsync(
        string ctorPath,
        object?[]? args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Constructs an object with a persistent callback that receives two
    /// callback-scoped typed JavaScript references.
    /// </summary>
    ValueTask<DomCallbackConstruction>
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
        where TSecond : class, IDomProxy;

    // ── Index access ─────────────────────────────────────────────────────────

    /// <summary>Reads a reviewed JSON-valued numeric index from a JS object.</summary>
    ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference,
        int index,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a reviewed JSON-valued string, number, or symbol key.</summary>
    ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference,
        object index,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a numeric index as a live JS object reference.</summary>
    ValueTask<IJSObjectReference> GetIndexRefAsync(
        IJSObjectReference reference,
        int index,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a string, number, or symbol key as a live JS reference.</summary>
    ValueTask<IJSObjectReference> GetIndexRefAsync(
        IJSObjectReference reference,
        object index,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a possibly-null indexed interface value as an owned typed proxy.
    /// </summary>
    ValueTask<TProxy?> GetIndexReferenceAsync<TProxy>(
        IJSObjectReference reference,
        int index,
        IDomProxyFactory proxyFactory,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>Writes a value at a numeric index on a JS object.</summary>
    ValueTask SetIndexAsync(
        IJSObjectReference reference,
        int index,
        object? value,
        CancellationToken cancellationToken = default);

    /// <summary>Writes a value to a string, number, or symbol key.</summary>
    ValueTask SetIndexAsync(
        IJSObjectReference reference,
        object index,
        object? value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens Blob or binary content already represented by a live JS reference
    /// through an owned, bounded stream.
    /// </summary>
    ValueTask<DomReadStream> OpenReadStreamAsync(
        IJSObjectReference reference,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken = default);

    // ── Event listeners ──────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a managed event listener to a live JS target.  The
    /// <paramref name="callback"/> receives the event payload serialised to
    /// JSON.  Dispose the returned <see cref="DomEventSubscription"/> to
    /// remove the listener and release the associated JS and dotnet resources.
    /// </summary>
    /// <param name="target">JS object that is the event target.</param>
    /// <param name="type">Event type string, e.g. <c>"click"</c>.</param>
    /// <param name="callback">
    /// Async delegate invoked for each event; receives serialised event JSON.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    ValueTask<DomEventSubscription> AddEventListenerAsync(
        IJSObjectReference target,
        string type,
        Func<string, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a typed event listener. Each event arrives as a borrowed proxy
    /// and is released after the awaited handler unless promoted.
    /// </summary>
    ValueTask<DomReferenceEventSubscription<TProxy>>
        AddReferenceEventListenerAsync<TProxy>(
            IJSObjectReference target,
            string type,
            IDomProxyFactory proxyFactory,
            DomTransportDescriptor transport,
            Func<DomBorrowedReference<TProxy>, Task> callback,
            CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;

    /// <summary>
    /// Attaches a listener from generated descriptor metadata, retaining the
    /// exact options with the established reference-listener registration.
    /// </summary>
    ValueTask<DomReferenceEventSubscription<TEvent>>
        SubscribeAsync<TEvent>(
            IJSObjectReference target,
            DomEventDescriptor<TEvent> descriptor,
            IDomProxyFactory proxyFactory,
            Func<DomBorrowedReference<TEvent>, Task> callback,
            DomEventListenerOptions? options = null,
            CancellationToken cancellationToken = default)
        where TEvent : class, IDomProxy;

    /// <summary>Attaches a descriptor-selected, proven JSON-valued listener.</summary>
    ValueTask<DomEventSubscription> SubscribeValueAsync<TValue>(
        IJSObjectReference target,
        DomEventDescriptor<TValue> descriptor,
        Func<TValue, Task> callback,
        DomEventListenerOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a previously registered event listener by its runtime ID.
    /// Called automatically by <see cref="DomEventSubscription.DisposeAsync"/>.
    /// </summary>
    ValueTask RemoveEventListenerAsync(
        int listenerId,
        CancellationToken cancellationToken = default);
}
