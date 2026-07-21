namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "LockManager",
    Implementation = "navigator.locks")]
public partial interface ILockManagerService;
