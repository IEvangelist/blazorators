# Blazor.OfflineStorage.WebAssembly

Generated Cache Storage and IndexedDB bindings with synchronous and asynchronous dispatch for Blazor WebAssembly.

Register the capability with `services.AddOfflineStorageCapability()` and inject `IOfflineStorageCapability`. Use `GetCacheStorage()` or `GetCacheStorageAsync()` for `window.caches`, and `GetIDBFactory()` or `GetIDBFactoryAsync()` for `window.indexedDB`.

The package is intentionally Window-profile only even though several underlying
contracts are also exposed in workers. Cache matches and enumerations return
owned live `IResponse`, `IRequest`, and browser-array proxies. IndexedDB
`IDBRequest<T>` results preserve live proxies for DOM contracts and use the
browser structured-clone boundary for TypeScript `any`/`object` values.

Cursor `key`/`primaryKey` are excluded because `IDBValidKey` includes recursive,
date, and binary union arms that do not yet have one safe inbound transport.
Request/cursor `source` is excluded because its TypeScript reference union does
not project to a single disposable proxy contract.
