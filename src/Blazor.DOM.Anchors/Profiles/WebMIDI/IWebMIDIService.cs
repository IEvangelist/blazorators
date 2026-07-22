namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "MIDIAccess",
    Implementation = "navigator.requestMIDIAccess")]
public partial interface IWebMIDIService;
