namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "StorageManager",
    Implementation = "navigator.storage")]
public partial interface IStorageManagementService;
