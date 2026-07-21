using Blazor.DOM;
using Microsoft.JSInterop;

namespace Blazor.DOM.WebAssembly.CompilationTests;

public static class GeneratedDomWasmConsumer
{
    public static async ValueTask ExerciseAsync(
        IBrowser browser,
        CancellationToken cancellationToken)
    {
        var window = await browser.GetWindowProxyAsync(cancellationToken);
        var document = await browser.GetDocumentProxyAsync(cancellationToken);
        var navigator = await browser.GetNavigatorProxyAsync(cancellationToken);

        _ = window.InnerWidth;
        _ = document.Title;
        _ = navigator.UserAgent;

        var blobFactory = window.BlobConstructor;
        await using var blob = blobFactory.Create(
            options: new BlobPropertyBag { Type = "text/plain" });
        _ = blob.Size;
        _ = await blob.TextAsync(cancellationToken);
        _ = await blob.BytesAsync(cancellationToken);

        await using var subscription = await window.SubscribeAsync(
            WindowEventMap.Click,
            static _ => Task.CompletedTask,
            cancellationToken: cancellationToken);
    }
}
