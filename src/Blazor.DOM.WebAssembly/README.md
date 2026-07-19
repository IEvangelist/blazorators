# Blazor.DOM.WebAssembly

JavaScript reference proxy runtime for exhaustive Blazor DOM bindings — WebAssembly flavour with synchronous dispatch paths.

## Overview

`Blazor.DOM.WebAssembly` extends the shared `Blazor.DOM` abstractions with an in-process runtime suitable for **Blazor WebAssembly**.  Non-Promise DOM operations expose synchronous C# paths via `IDomSyncRuntime`; Promise-returning operations remain asynchronous.

## Key services

| Service | Description |
|---|---|
| `IBrowser` | Entry point for common browser globals. |
| `IDomRuntime` | Async dispatch (shared abstraction). |
| `IDomSyncRuntime` | Sync dispatch extensions for non-Promise DOM ops. |
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

- Do **not** use this package together with `Blazor.DOM` (Server) in the same application.
- Call at least one async DOM method before using synchronous dispatch to ensure the JS module is loaded.
- `WasmDomProxyBase` extends `DomProxyBase` with `SyncRuntime` and `InProcessReference` accessors.
