# Blazor.BrowserCoordination.WebAssembly

Generated Broadcast Channel, Web Locks, and page visibility bindings with async and in-process sync roots for Blazor WebAssembly.

Register the capability with `services.AddBrowserCoordinationCapability()` and inject `IBrowserCoordinationCapability`. The capability exposes the `BroadcastChannel` constructor, `navigator.locks`, and `document` through explicit feature-detection roots.

Web Locks requires a secure context. Broadcast Channel and page visibility do not, but the combined capability reports `RequiresSecureContext` because one of its roots has that requirement. The focused document surface contains only `hidden`, `visibilityState`, and `visibilitychange`; unrelated document APIs and lifecycle events absent from the authoritative semantic model are excluded.
