namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "MediaSession",
    Implementation = "navigator.mediaSession")]
public partial interface IMediaSessionService;
