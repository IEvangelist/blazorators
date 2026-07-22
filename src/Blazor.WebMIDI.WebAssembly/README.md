# Blazor.WebMIDI.WebAssembly

Focused Web MIDI bindings for Blazor WebAssembly. Non-Promise operations use
in-process dispatch while `navigator.requestMIDIAccess`, port open, and port
close preserve their asynchronous Promise behavior.

```csharp
builder.Services.AddWebMIDICapability();

var access = await capability.RequestMIDIAccessAsync();
```

Web MIDI requires a secure context and browser permission. System-exclusive
messages may require the additional `midi-sysex` permission policy.
