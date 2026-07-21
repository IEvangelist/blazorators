// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Options retained with a typed DOM event registration.</summary>
public sealed record DomEventListenerOptions
{
    /// <summary>Uses the capture phase.</summary>
    public bool Capture { get; init; }

    /// <summary>Removes the listener after its first invocation.</summary>
    public bool? Once { get; init; }

    /// <summary>Declares that the listener will not cancel the event.</summary>
    public bool? Passive { get; init; }

    /// <summary>An optional live AbortSignal proxy.</summary>
    public IDomProxy? Signal { get; init; }

    /// <summary>Preserves the TypeScript boolean-capture options form.</summary>
    public static implicit operator DomEventListenerOptions(bool capture) =>
        new() { Capture = capture };

    internal object ToInteropValue() => new
    {
        capture = Capture,
        once = Once,
        passive = Passive,
        signal = Signal?.Reference,
    };
}

/// <summary>
/// Shared strongly typed subscription surface implemented by generated event-target proxies.
/// </summary>
public interface IDomEventTargetProxy : IDomProxy
{
    /// <summary>
    /// Subscribes using a generated descriptor. The returned handle owns the
    /// listener registration and must be asynchronously disposed.
    /// </summary>
    [DomEventSubscription(
        DomEventSubscriptionOperation.Subscribe,
        SupportsOmittedOptions = true,
        SupportsBooleanCapture = true,
        SupportsObjectOptions = true)]
    ValueTask<DomReferenceEventSubscription<TEvent>> SubscribeAsync<TEvent>(
        DomEventDescriptor<TEvent> descriptor,
        Func<DomBorrowedReference<TEvent>, Task> callback,
        DomEventListenerOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomProxy;

    /// <summary>Subscribes to an event whose payload is a proven JSON value.</summary>
    [DomEventSubscription(
        DomEventSubscriptionOperation.Subscribe,
        SupportsOmittedOptions = true,
        SupportsBooleanCapture = true,
        SupportsObjectOptions = true)]
    ValueTask<DomEventSubscription> SubscribeValueAsync<TValue>(
        DomEventDescriptor<TValue> descriptor,
        Func<TValue, Task> callback,
        DomEventListenerOptions? options = null,
        CancellationToken cancellationToken = default);
}
