# Blazor.MediaSession.WebAssembly

Generated Media Session API bindings for Blazor WebAssembly with synchronous dispatch for synchronous Web API members and asynchronous Promise/callback operations.

Register the capability with `services.AddMediaSessionCapability()` and inject `IMediaSessionCapability`. The explicit root is `navigator.mediaSession`; metadata, playback/position state, capture-state Promises, action enums, and typed action callbacks match the Server package logically.

Asynchronously dispose every `DomCallbackRegistration` to clear its browser handler, and dispose owned media-session and metadata proxies when finished.
