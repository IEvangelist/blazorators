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

    /// <summary>Reads a named property from a live JS object.</summary>
    ValueTask<TValue> GetPropertyAsync<TValue>(
        IJSObjectReference reference,
        string name,
        CancellationToken cancellationToken = default);

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

    // ── Global access ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a dotted global path (e.g. <c>"document"</c>) and returns a
    /// JS reference to the object.
    /// </summary>
    ValueTask<IJSObjectReference> GetGlobalAsync(
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

    // ── Index access ─────────────────────────────────────────────────────────

    /// <summary>Reads a numeric or string index from a JS object.</summary>
    ValueTask<TValue> GetIndexAsync<TValue>(
        IJSObjectReference reference,
        int index,
        CancellationToken cancellationToken = default);

    /// <summary>Writes a value at a numeric index on a JS object.</summary>
    ValueTask SetIndexAsync(
        IJSObjectReference reference,
        int index,
        object? value,
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
    /// Removes a previously registered event listener by its runtime ID.
    /// Called automatically by <see cref="DomEventSubscription.DisposeAsync"/>.
    /// </summary>
    ValueTask RemoveEventListenerAsync(
        int listenerId,
        CancellationToken cancellationToken = default);
}
