namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "CredentialsContainer",
    Implementation = "navigator.credentials")]
public partial interface ICredentialsService;
