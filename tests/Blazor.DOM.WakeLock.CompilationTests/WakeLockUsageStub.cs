// D1 regression: verifies all three AddEventListener/RemoveEventListener overloads compile
// without CS0121 ambiguity when calling with 2 args or explicit 3rd arg.

namespace Blazor.DOM.WakeLock.CompilationTests;

internal static class WakeLockUsageStub
{
    internal static void Verify(
        IWakeLockSentinel target,
        EventListenerOrEventListenerObject callback)
    {
        // Overload 1: no third param (2-arg call must not be ambiguous)
        target.AddEventListener("type", callback);
        target.RemoveEventListener("type", callback);

        // Overload 2: explicit bool (required param — no ambiguity)
        target.AddEventListener("type", callback, true);
        target.RemoveEventListener("type", callback, false);

        // Overload 3: explicit options (required nullable — no ambiguity)
        target.AddEventListener("type", callback, (AddEventListenerOptions?)null);
        target.RemoveEventListener("type", callback, (EventListenerOptions?)null);
    }
}
