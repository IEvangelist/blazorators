# Blazor.DOM

JavaScript reference proxy runtime for exhaustive Blazor DOM bindings — Server / hosting-neutral async flavour.

## Overview

`Blazor.DOM` provides the low-level runtime infrastructure that generated DOM proxy types build on top of.  It targets **Blazor Server** and any hosting model where JavaScript interop is inherently asynchronous.

## Key services

| Service | Description |
|---|---|
| `IBrowser` | Entry point for common browser globals (`window`, `document`, `navigator`, arbitrary path). |
| `IDomRuntime` | Core async dispatch — property get/set, method invocation, constructor, index access, event subscribe/unsubscribe. |
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
        var docRef = await Browser.GetDocumentAsync();
        // Pass docRef to IDomProxyFactory.Create<HtmlDocument>(docRef)
        // when generated proxy types are available.
    }
}
```

## Ownership semantics

- `DomProxyBase.DisposeAsync()` disposes the underlying `IJSObjectReference`.
- `DomObjectReference.Owned(ref)` disposes the wrapped reference; `Shared(ref)` does not.
- `DomEventSubscription.DisposeAsync()` removes the JS listener and releases the dotnet callback reference; idempotent.

## Notes

- Do **not** use this package together with `Blazor.DOM.WebAssembly` in the same application.
- Do **not** inject or use `IBrowser`/`IDomRuntime` during Blazor prerendering; the first JS call will throw `DomJSException` with a helpful message.
