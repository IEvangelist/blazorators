# Blazor.Notifications.WebAssembly

Generated Notifications API bindings with synchronous and asynchronous dispatch for Blazor WebAssembly.

Register the capability with `services.AddNotificationsCapability()` and inject `INotificationsCapability`. The explicit `Notification` root resolves the typed `INotificationFactory`, which exposes the corpus-backed constructor, static permission state, and permission request path. Constructed notifications expose typed options, lifecycle events, unsubscribe operations, close, and proxy disposal.

The generated metadata provides feature-detection paths and marks secure-context, user-activation, and notifications-permission requirements.
