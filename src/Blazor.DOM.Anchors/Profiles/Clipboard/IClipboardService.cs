namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Clipboard",
    Implementation = "navigator.clipboard")]
public partial interface IClipboardService;
