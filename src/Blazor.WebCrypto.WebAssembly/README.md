# Blazor.WebCrypto.WebAssembly

Generated Web Crypto API bindings for Blazor WebAssembly.

Register with `services.AddWebCryptoCapability()` and inject `IWebCryptoCapability`. Explicit `crypto` and `crypto.subtle` roots provide synchronous root access and asynchronous Promise operations while preserving CryptoKey proxies, typed algorithm unions and dictionaries, key usages, and binary BufferSource transport.

The profile uses the format-specific `exportKey` overloads and the symmetric `generateKey` overload. It excludes the broad mixed binary/JSON export union and asymmetric `CryptoKeyPair` generation overloads because nested live CryptoKey references cannot be safely returned as a JSON dictionary.
