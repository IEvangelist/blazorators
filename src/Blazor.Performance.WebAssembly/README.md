# Blazor.Performance.WebAssembly

Generated Performance Timeline, User Timing, Performance Observer, Navigation Timing, and Resource Timing bindings with async and in-process sync roots for Blazor WebAssembly.

Register the capability with `services.AddPerformanceCapability()` and inject `IPerformanceCapability`. The capability exposes the global `performance` object and the `PerformanceObserver` constructor through explicit feature-detection roots.

The focused profile includes marks, measures, typed entry contracts, observer callbacks and buffering options, legacy navigation timing, and modern navigation/resource timing. Unrelated event timing and broader DOM surfaces are intentionally excluded.
