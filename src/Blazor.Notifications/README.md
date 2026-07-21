# Blazor.Notifications

Generated Notifications API bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register the capability with `services.AddNotificationsCapability()` and inject `INotificationsCapability`. The explicit `Notification` root resolves the typed `INotificationFactory`, which exposes the corpus-backed constructor, static permission state, and permission request path. Constructed notifications expose typed options, lifecycle events, unsubscribe operations, close, and proxy disposal.

The generated metadata provides feature-detection paths and marks secure-context, user-activation, and notifications-permission requirements.
