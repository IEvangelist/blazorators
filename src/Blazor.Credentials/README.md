# Blazor.Credentials

Generated Credential Management and WebAuthn bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register with `services.AddCredentialsCapability()` and inject `ICredentialsCapability`. The capability resolves `navigator.credentials` as an `ICredentialsContainer` live proxy. Creation and request options remain typed dictionaries, while credential, public-key credential, and authenticator response values retain live-reference semantics and ArrayBuffer values use binary transport.

The profile intentionally excludes `PublicKeyCredential.toJSON()` because the platform declaration returns untyped `any`, and `getClientExtensionResults()` because its nested binary output dictionary has no safe direct return transport. Typed extension input and output contracts are still included.
