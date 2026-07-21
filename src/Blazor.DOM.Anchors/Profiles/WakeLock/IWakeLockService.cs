namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "WakeLock",
    Implementation = "navigator.wakeLock")]
public partial interface IWakeLockService;
