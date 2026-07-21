namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "MediaDevices",
    Implementation = "navigator.mediaDevices")]
public partial interface IMediaDevicesService;
