# Blazor.DOM

JavaScript reference proxy runtime for exhaustive Blazor DOM bindings — Server / hosting-neutral async flavour.

## Overview

`Blazor.DOM` provides the exhaustive generated Server/hosting-neutral DOM API and its runtime. It targets **Blazor Server** and hosting models where JavaScript interop is inherently asynchronous. Non-Promise members are cancellable `ValueTask` methods; Promise members are awaited through the same async surface.

## Build-time generation

DOM contracts are generated during build into `artifacts/obj/Blazor.DOM.Generation/<configuration>/dom`; generated C# is not checked in. `Blazor.DOM.Generation.csproj` is the single incremental generation node shared by exhaustive, WebAssembly, and focused packages, so multi-target builds reuse one strict semantic projection. Its inputs are the manifest-locked IR in `data/Blazor.DOM`, profile definitions in `data/Blazor.DOM.Profiles`, handwritten `[JSAutoInterop]` roots in `src/Blazor.DOM.Anchors`, and the strict emitter source.

The existing Roslyn generator remains responsible for its narrower declaration-driven services. The exhaustive DOM surface uses the strict semantic IR/emitter because it preserves merged declarations, advanced types, host parity, and exact zero-deferral accounting that the older projection does not model. Package projects import `Blazor.DOM.Generation.targets` to select their host/profile slice and pack manifests directly from intermediate output.

## Key services

| Service | Description |
|---|---|
| `IBrowser` | Single DI root for typed `window`, `document`, `navigator`, and authoritative global paths. Injection is prerender-safe; the first operation performs JS interop. |
| `IDomRuntime` | Core async dispatch — validated JSON, typed references/callbacks, bounded streams, constructor/index access, and event subscriptions. |
| `IDomProxyFactory` | Typed proxy registry — maps `IJSObjectReference` handles to generated C# proxy instances without reflection. |

## Getting started

```csharp
// Program.cs / Startup.cs
builder.Services.AddBlazorDOM();
```

```razor
@inject IBrowser Browser
@inject IDomProxyFactory ProxyFactory

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var document = await Browser.GetDocumentProxyAsync();
        var title = await document.GetTitleAsync();
    }
}
```

## Ownership semantics

- `DomProxyBase.DisposeAsync()` disposes the underlying `IJSObjectReference`.
- `DomObjectReference.Owned(ref)` disposes the wrapped reference; `Shared(ref)` does not.
- `DomEventSubscription.DisposeAsync()` removes the JS listener and releases the dotnet callback reference; idempotent.
- `DomBorrowedReference<TProxy>` is callback-scoped. The runtime releases it after the awaited handler. `Promote()` transfers ownership only if that handler completes successfully.
- `DomReadStream` owns its bounded `Stream` and any backing `IJSStreamReference`; use `await using`. Empty values use an owned zero-length stream without manufacturing an invalid JS stream reference.

## Typed transport

Generated members consume `DomTransportDescriptor` metadata with one of six explicit kinds: JSON value, JS reference, JS stream, binary, transferable, or unsupported. Named DOM interfaces are always live proxies. Generated property, method, and index results use `GetPropertyReferenceAsync<TProxy>`, `InvokeMethodReferenceAsync<TProxy>`, and `GetIndexReferenceAsync<TProxy>` so nullable interface values cross through `DotNet.createJSObjectReference` only when non-null. Web IDL `[Serializable]` only records structured-clone support, so a `Blob` remains a JS reference rather than becoming a JSON object.

JSON dispatch accepts primitives, enums, string-keyed dictionaries and sequences whose nested values are also JSON, and generated/reviewed DTOs marked with `[DomJsonValue]`. Container validation rejects cycles and more than 29 nested user containers. This conservative common limit derives from the `JSRuntime` serializer depth of 32 on .NET 8–10 and reserves two levels for the outer interop and method-argument arrays plus one level for the terminal value; property and index payloads use the same safe limit. `any`, `unknown`, and `object` members use `DomDynamicValue` to select and validate an explicit transport. Unsupported, semantically ambiguous, or mixed shapes throw `DomTransportException` before invocation.

Reference callbacks and typed events pass `DotNet.createJSObjectReference(...)` values to `IJSObjectReference`, then wrap them through `IDomProxyFactory`. Event registrations are callback-delivered so cancellation and registration failures roll back listeners and references. The existing `Func<string, Task>` event API remains a compatibility snapshot path; `AddReferenceEventListenerAsync<TProxy>` is the live typed path for generated events.

Blob, ArrayBuffer, and typed-array bytes use `DotNet.createJSStreamReference(...)` and `IJSStreamReference`. `OpenReadStreamAsync`, `InvokeMethodStreamAsync`, and `InvokeMethodNullableStreamAsync` require an explicit maximum length and propagate cancellation without base64 or JSON buffering. The nullable API distinguishes a null result from a non-null zero-length stream. The original typed proxy remains available for normal DOM operations.

## Notes

- Do **not** reference this package together with `Blazor.DOM.WebAssembly`; both intentionally provide the same generated namespaces with host-specific signatures.
- `IBrowser` may be injected during prerendering. Defer its first operation until interactive rendering; an attempted prerender operation throws `DomJSException` with a clear message.
- Browser E2E acceptance for callback-created object/stream references belongs in the combined generated-DOM fixture once those contracts are integrated.
