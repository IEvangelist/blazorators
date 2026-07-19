// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Wraps a JavaScript exception with DOM-specific context so callers get
/// actionable information about which object path / method raised the error.
/// </summary>
public sealed class DomJSException : Exception
{
    /// <summary>Initialises a contextual DOM exception.</summary>
    /// <param name="message">Human-readable message.</param>
    /// <param name="innerException">Originating exception, if any.</param>
    public DomJSException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a <see cref="DomJSException"/> for a prerendering violation.
    /// </summary>
    public static DomJSException Prerendering() =>
        new(
            "DOM interop is not available during prerendering. " +
            "Ensure your component uses OnAfterRenderAsync or guards JS calls " +
            "behind a check that interactivity is available (e.g., firstRender).");

    /// <summary>
    /// Creates a <see cref="DomJSException"/> wrapping a JS exception with
    /// optional context about the call site.
    /// </summary>
    public static DomJSException FromJSException(
        JSException inner, string? objectPath = null, string? memberName = null)
    {
        var context = (objectPath, memberName) switch
        {
            (not null, not null) => $" [{objectPath}.{memberName}]",
            (not null, null)     => $" [{objectPath}]",
            (null, not null)     => $" [{memberName}]",
            _                    => string.Empty
        };
        return new DomJSException(
            $"JavaScript error in DOM interop{context}: {inner.Message}", inner);
    }
}
