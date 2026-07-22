namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Navigator",
    Implementation = "navigator.getGamepads",
    EntryPointName = "Gamepads",
    MemberName = "getGamepads")]
public partial interface IGamepadService;
