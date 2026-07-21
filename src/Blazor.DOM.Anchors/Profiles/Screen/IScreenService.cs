namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Screen",
    Implementation = "window.screen")]
public partial interface IScreenService;
