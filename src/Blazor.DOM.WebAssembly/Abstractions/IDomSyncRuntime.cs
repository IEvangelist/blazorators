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
    /// <summary>Resolves an authoritative global path synchronously.</summary>
    IJSInProcessObjectReference GetGlobalRef(string path);

    /// <summary>Constructs a JavaScript object synchronously.</summary>
    IJSInProcessObjectReference ConstructRef(string constructorPath, object?[]? args);

    // ── Sync property access ─────────────────────────────────────────────────

    /// <summary>Reads a reviewed JSON-valued property synchronously.</summary>
    TValue GetProperty<TValue>(
        IJSInProcessObjectReference reference,
        string name,
        bool allowStructuredClone = false);

    /// <summary>Reads a named property synchronously as a live JS object reference.</summary>
    IJSInProcessObjectReference GetPropertyRef(
        IJSInProcessObjectReference reference,
        string name);

    /// <summary>Writes a named property synchronously.</summary>
    void SetProperty(IJSInProcessObjectReference reference, string name, object? value);

    // ── Sync method invocation ───────────────────────────────────────────────

    /// <summary>
    /// Invokes a method synchronously and deserialises the return value as
    /// <typeparamref name="TResult"/>.
    /// </summary>
    TResult InvokeMethod<TResult>(
        IJSInProcessObjectReference reference,
        string name,
        object?[]? args,
        bool allowStructuredClone = false);

    /// <summary>Invokes a void method synchronously.</summary>
    void InvokeMethodVoid(
        IJSInProcessObjectReference reference, string name, object?[]? args);

    /// <summary>
    /// Invokes a method synchronously and returns a live JS object reference.
    /// </summary>
    IJSInProcessObjectReference InvokeMethodRef(
        IJSInProcessObjectReference reference, string name, object?[]? args);

    // ── Sync index access ────────────────────────────────────────────────────

    /// <summary>Reads a reviewed JSON-valued numeric index synchronously.</summary>
    TValue GetIndex<TValue>(IJSInProcessObjectReference reference, int index);

    /// <summary>Reads a reviewed JSON-valued string, number, or symbol key.</summary>
    TValue GetIndex<TValue>(IJSInProcessObjectReference reference, object index);

    /// <summary>Reads a numeric index synchronously as a live JS object reference.</summary>
    IJSInProcessObjectReference GetIndexRef(
        IJSInProcessObjectReference reference,
        int index);

    /// <summary>Reads a string, number, or symbol key as a live JS reference.</summary>
    IJSInProcessObjectReference GetIndexRef(
        IJSInProcessObjectReference reference,
        object index);

    /// <summary>Writes a numeric index synchronously.</summary>
    void SetIndex(IJSInProcessObjectReference reference, int index, object? value);

    /// <summary>Writes a value to a string, number, or symbol key.</summary>
    void SetIndex(IJSInProcessObjectReference reference, object index, object? value);
}
