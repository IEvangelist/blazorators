using Blazor.DOM;
using Microsoft.JSInterop;

namespace Blazor.DOM.Server.CompilationTests;

public static class GeneratedDomServerConsumer
{
    public static async ValueTask ExerciseAsync(
        IBrowser browser,
        IDomRuntime runtime,
        CancellationToken cancellationToken)
    {
        var window = await browser.GetWindowProxyAsync(cancellationToken);
        var document = await browser.GetDocumentProxyAsync(cancellationToken);
        var navigator = await browser.GetNavigatorProxyAsync(cancellationToken);

        _ = await window.GetInnerWidthAsync(cancellationToken);
        _ = await document.GetTitleAsync(cancellationToken);
        _ = await navigator.GetUserAgentAsync(cancellationToken);

        var blobFactory =
            await window.GetBlobConstructorAsync(cancellationToken);
        await using var blob = await blobFactory.CreateAsync(
            options: new BlobPropertyBag { Type = "text/plain" },
            cancellationToken: cancellationToken);
        _ = await blob.GetSizeAsync(cancellationToken);
        _ = await blob.TextAsync(cancellationToken);
        _ = await blob.BytesAsync(cancellationToken);

        await using var subscription = await window.SubscribeAsync(
            WindowEventMap.Click,
            static _ => Task.CompletedTask,
            cancellationToken: cancellationToken);

        await using var stream = await runtime.OpenReadStreamAsync(
            blob.Reference,
            DomTransportDescriptor.JsReference(
                "Blob",
                streamable: true,
                structuredClone: true),
            maximumLength: 1024,
            cancellationToken);
    }
}
