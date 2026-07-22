# Blazor.Gamepad

Generated Gamepad API bindings for Blazor Server and hosting-neutral async JavaScript interop.

Register the capability with `services.AddGamepadCapability()` and inject `IGamepadCapability`. `GetGamepadsAsync()` invokes the explicit `navigator.getGamepads` entry point and returns an owned live browser array whose nullable elements are owned typed `IGamepad` proxies. `GetWindowAsync()` exposes typed `gamepadconnected` and `gamepaddisconnected` subscriptions with async-disposable registrations.

Controller state preserves numeric axis snapshots, live button and haptic actuator references, Promise-based haptic results, and exact string enums. The capability metadata records the user-interaction requirement. Dispose arrays, controller/event proxies, haptic actuators, and subscriptions when finished.
