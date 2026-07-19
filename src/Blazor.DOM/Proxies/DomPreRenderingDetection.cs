// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Narrow detection logic for conditions that indicate JS interop is not yet
/// available because the component tree is being pre-rendered server-side.
/// </summary>
internal static class DomPreRenderingDetection
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ex"/> carries a
    /// message that indicates the circuit/runtime is in the pre-rendering phase
    /// and JS interop calls are therefore not yet permitted.
    /// </summary>
    internal static bool IsPreRendering(Exception ex) =>
        ex.Message.Contains("prerendering", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("JavaScript interop calls cannot be issued at this time",
            StringComparison.OrdinalIgnoreCase);
}
