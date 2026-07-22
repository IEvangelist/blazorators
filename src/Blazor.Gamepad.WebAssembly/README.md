# Blazor.Gamepad.WebAssembly

Generated Gamepad API bindings for Blazor WebAssembly with synchronous member dispatch where the Web API is synchronous and asynchronous entry-point/reference ownership.

Register the capability with `services.AddGamepadCapability()` and inject `IGamepadCapability`. `GetGamepadsAsync()` invokes `navigator.getGamepads` and returns an owned live browser array of nullable typed controller proxies. `GetWindow()`/`GetWindowAsync()` provide typed connection-event subscriptions.

Dispose arrays, controller/event proxies, haptic actuators, and subscriptions when finished.
