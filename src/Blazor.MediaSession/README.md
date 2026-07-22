# Blazor.MediaSession

Generated Media Session API bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register the capability with `services.AddMediaSessionCapability()` and inject `IMediaSessionCapability`. The explicit root is `navigator.mediaSession`; generated contracts preserve typed metadata, playback and position state, Promise-based camera/microphone activity, and exact action/playback enums.

`SetActionHandlerAsync` returns an owned `DomCallbackRegistration`; asynchronously dispose it to clear the browser action handler and release its managed callback reference. `ClearActionHandlerAsync` preserves the Web API's explicit null-handler behavior. Dispose media-session and metadata proxies when finished.
