#nullable enable

using Microsoft.JSInterop;

namespace Blazor.DOM.TypedEvents.CompilationTests;

public static class TypedEventsConsumer
{
    public static DomEventDescriptor<IPointerEvent> InheritedClick =>
        HTMLElementEventMap.Click;

    public static ValueTask<DomReferenceEventSubscription<IEvent>>
        SubscribeAbortAsync(
            IAbortSignal target,
            Func<DomBorrowedReference<IEvent>, Task> handler,
            CancellationToken cancellationToken = default)
    {
        DomEventListenerOptions options = true;
        options = options with { Once = true, Passive = false };
        return target.SubscribeAsync(
            AbortSignalEventMap.Abort,
            handler,
            options,
            cancellationToken);
    }

    public static ValueTask DisposeAsync(
        DomReferenceEventSubscription<IEvent> subscription) =>
        subscription.DisposeAsync();
}
