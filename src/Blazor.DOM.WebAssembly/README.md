# Blazor.DOM.WebAssembly

JavaScript reference proxy runtime for exhaustive Blazor DOM bindings — WebAssembly flavour with synchronous dispatch paths.

## Overview

`Blazor.DOM.WebAssembly` is the mutually exclusive in-process package for **Blazor WebAssembly**. It contains the shared value/runtime corpus directly rather than depending on `Blazor.DOM`. Non-Promise DOM operations are synchronous; Promise and lifecycle operations remain cancellable and asynchronous.

## Key services

| Service | Description |
|---|---|
| `IBrowser` | Entry point for common browser globals. |
| `IDomRuntime` | Async validated JSON, typed reference/callback, and bounded stream dispatch. |
| `IDomSyncRuntime` | Sync validated JSON/reference dispatch for non-Promise DOM ops. |
| `IDomProxyFactory` | Typed proxy registry. |

## Getting started

```csharp
// Program.cs
builder.Services.AddBlazorDOMWebAssembly();
```

```razor
@inject IBrowser Browser
@inject IDomSyncRuntime SyncRuntime

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        // First call initialises the module asynchronously:
        var docRef = await Browser.GetDocumentAsync() as IJSInProcessObjectReference;
        // After this, sync calls work:
        // var title = SyncRuntime.GetProperty<string>(docRef!, "title");
    }
}
```

## Notes

- Do **not** reference this package together with `Blazor.DOM`; both intentionally provide the same generated namespaces with different host signatures.
- Call at least one async DOM method before using synchronous dispatch to ensure the JS module is loaded.
- `WasmDomProxyBase` extends `DomProxyBase` with `SyncRuntime` and `InProcessReference` accessors.
- Transport classification and ownership match `Blazor.DOM`: interfaces use explicit synchronous/asynchronous live-reference paths, nullable interface results use the typed async reference-result APIs, callback references are borrowed unless promoted, and Blob/ArrayBuffer/typed-array bytes use bounded `IJSStreamReference` reads with cancellation and async disposal. Empty values use an owned zero-length stream without an invalid JS stream reference.
- The legacy JSON event snapshot remains available, while generated event contracts should use `AddReferenceEventListenerAsync<TProxy>`.
