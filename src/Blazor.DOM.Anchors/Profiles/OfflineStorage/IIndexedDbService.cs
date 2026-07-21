namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "IDBFactory",
    Implementation = "window.indexedDB")]
public partial interface IIndexedDbService;
