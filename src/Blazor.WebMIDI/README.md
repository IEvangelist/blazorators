# Blazor.WebMIDI

Focused Web MIDI bindings for Blazor Server and hosting-neutral asynchronous
interop. Contracts and manifests are generated from the checked-in `WebMIDI`
profile during the build.

```csharp
builder.Services.AddWebMIDICapability();

var access = await capability.RequestMIDIAccessAsync(
    new MIDIOptions { Sysex = true });
```

Web MIDI requires a secure context and browser permission. System-exclusive
messages may require the additional `midi-sysex` permission policy.
