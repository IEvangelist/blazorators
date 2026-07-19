// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Extends <see cref="IDomRuntime"/> with synchronous dispatch paths available
/// on Blazor WebAssembly where the JS engine runs in-process.
/// Non-Promise DOM operations MUST use these synchronous methods when targeting
/// WASM; Promise-returning operations use the async paths on the base interface.
/// </summary>
public interface IDomSyncRuntime : IDomRuntime
{
    // ── Sync property access ─────────────────────────────────────────────────

    /// <summary>Reads a named property synchronously.</summary>
    TValue GetProperty<TValue>(IJSInProcessObjectReference reference, string name);

    /// <summary>Writes a named property synchronously.</summary>
    void SetProperty(IJSInProcessObjectReference reference, string name, object? value);

    // ── Sync method invocation ───────────────────────────────────────────────

    /// <summary>
    /// Invokes a method synchronously and deserialises the return value as
    /// <typeparamref name="TResult"/>.
    /// </summary>
    TResult InvokeMethod<TResult>(
        IJSInProcessObjectReference reference, string name, object?[]? args);

    /// <summary>Invokes a void method synchronously.</summary>
    void InvokeMethodVoid(
        IJSInProcessObjectReference reference, string name, object?[]? args);

    /// <summary>
    /// Invokes a method synchronously and returns a live JS object reference.
    /// </summary>
    IJSInProcessObjectReference InvokeMethodRef(
        IJSInProcessObjectReference reference, string name, object?[]? args);

    // ── Sync index access ────────────────────────────────────────────────────

    /// <summary>Reads a numeric index synchronously.</summary>
    TValue GetIndex<TValue>(IJSInProcessObjectReference reference, int index);

    /// <summary>Writes a numeric index synchronously.</summary>
    void SetIndex(IJSInProcessObjectReference reference, int index, object? value);
}
