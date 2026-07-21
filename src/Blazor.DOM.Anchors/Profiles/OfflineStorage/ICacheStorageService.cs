namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "CacheStorage",
    Implementation = "window.caches")]
public partial interface ICacheStorageService;
