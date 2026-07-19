// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Base class for generated DOM proxy types on Blazor WebAssembly.  Extends
/// <see cref="DomProxyBase"/> with access to the synchronous runtime and the
/// in-process object reference required for sync dispatch.
/// </summary>
public abstract class WasmDomProxyBase : DomProxyBase
{
    /// <summary>
    /// The synchronous runtime.  Obtained by casting the base
    /// <see cref="DomProxyBase.Runtime"/> to <see cref="IDomSyncRuntime"/>;
    /// throws <see cref="InvalidOperationException"/> when not running on WASM.
    /// </summary>
    protected IDomSyncRuntime SyncRuntime =>
        Runtime as IDomSyncRuntime
        ?? throw new InvalidOperationException(
            $"{nameof(IDomSyncRuntime)} is only available on Blazor WebAssembly. " +
            "Use async methods instead.");

    /// <summary>
    /// The in-process JS object reference required for synchronous dispatch.
    /// Throws when the reference was not acquired from an in-process runtime
    /// (which should not happen on WASM).
    /// </summary>
    protected IJSInProcessObjectReference InProcessReference =>
        Reference as IJSInProcessObjectReference
        ?? throw new InvalidOperationException(
            "A synchronous DOM operation requires an IJSInProcessObjectReference, " +
            "but the proxy was constructed with a non-in-process reference. " +
            "Ensure the WASM DOM runtime services are registered via AddBlazorDOMWebAssembly().");

    /// <param name="reference">Owned in-process JS object reference.</param>
    /// <param name="runtime">WASM-capable sync runtime.</param>
    /// <param name="factory">Proxy factory for child proxy creation.</param>
    protected WasmDomProxyBase(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
        : base(reference, runtime, factory)
    {
    }
}
