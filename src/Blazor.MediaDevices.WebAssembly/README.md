# Blazor.MediaDevices.WebAssembly

Generated Media Capture and Streams API bindings with synchronous and asynchronous dispatch for Blazor WebAssembly.

Register the capability with `services.AddMediaDevicesCapability()` and inject `IMediaDevicesCapability`. The explicit root is `navigator.mediaDevices`; generated contracts cover device enumeration, user/display capture, media streams and tracks, constraints, settings, capabilities, and device/track events.

The generated metadata marks secure-context and user-activation requirements plus camera, microphone, and display-capture permissions. Returned streams, tracks, and devices are typed disposable proxies.
