# Blazor.Credentials.WebAssembly

Generated Credential Management and WebAuthn bindings for Blazor WebAssembly.

Register with `services.AddCredentialsCapability()` and inject `ICredentialsCapability`. The capability exposes synchronous and asynchronous access to `navigator.credentials` while preserving typed options, live credential and authenticator proxies, and binary ArrayBuffer transport.

The profile intentionally excludes `PublicKeyCredential.toJSON()` because the platform declaration returns untyped `any`, and `getClientExtensionResults()` because its nested binary output dictionary has no safe direct return transport. Typed extension input and output contracts are still included.
