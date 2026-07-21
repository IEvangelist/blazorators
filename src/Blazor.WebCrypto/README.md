# Blazor.WebCrypto

Generated Web Crypto API bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register with `services.AddWebCryptoCapability()` and inject `IWebCryptoCapability`. Explicit `crypto` and `crypto.subtle` roots preserve Promise calls, CryptoKey live proxies, typed algorithms and key usages, and BufferSource/ArrayBuffer binary transport.

The profile uses the format-specific `exportKey` overloads and the symmetric `generateKey` overload. It excludes the broad mixed binary/JSON export union and asymmetric `CryptoKeyPair` generation overloads because nested live CryptoKey references cannot be safely returned as a JSON dictionary.
