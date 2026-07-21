# Blazor.BrowserCoordination

Generated Broadcast Channel, Web Locks, and page visibility bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register the capability with `services.AddBrowserCoordinationCapability()` and inject `IBrowserCoordinationCapability`. The capability exposes the `BroadcastChannel` constructor, `navigator.locks`, and `document` through explicit feature-detection roots.

Web Locks requires a secure context. Broadcast Channel and page visibility do not, but the combined capability reports `RequiresSecureContext` because one of its roots has that requirement. The focused document surface contains only `hidden`, `visibilityState`, and `visibilitychange`; unrelated document APIs and lifecycle events absent from the authoritative semantic model are excluded.
