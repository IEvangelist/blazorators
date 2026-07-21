namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "SubtleCrypto",
    Implementation = "crypto.subtle")]
public partial interface ISubtleCryptoService;
