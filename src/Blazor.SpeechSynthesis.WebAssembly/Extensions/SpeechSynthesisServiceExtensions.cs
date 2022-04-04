// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace Microsoft.JSInterop;

/// <summary>
/// Extension methods for the <see cref="ISpeechSynthesisService"/> functionality.
/// </summary>
public static class SpeechSynthesisServiceExtensions
{
    readonly static ConcurrentDictionary<Guid, Func<Task>> s_callbackRegistry = new();

    /// <summary>
    /// Removes the subscription, assigns <c>speechSynthesis.onvoiceschanged</c> to <c>null</c>.
    /// </summary>
    /// <param name="service">
    /// The current <paramref name="service"/> instance to unsubscribe the
    /// <c>speechSynthesis.onvoiceschanged</c> callback from.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// When the <paramref name="service"/> instance doesn't support the event
    /// callback, this error is thrown.
    /// </exception>
    public static void UnsubscribeFromVoicesChanged(
        this ISpeechSynthesisService service)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } } svc)
        {
            svc._javaScript.InvokeVoid(
                "blazorators.speechSynthesis.unsubscribeVoicesChanged");
        }
        else
        {
            throw new InvalidOperationException(
                "SpeechSynthesisService is not available.");
        }
    }

    /// <summary>
    /// A method used to register a callback for when the speech
    /// synthesis service underlying voices have changed.
    /// </summary>
    /// <param name="service">
    /// The current <paramref name="service"/> instance to register the
    /// <c>speechSynthesis.onvoiceschanged</c> callback to.
    /// </param>
    /// <param name="onVoicesChanged">
    /// The callback to invoke when the <c>speechSynthesis.onvoiceschanged</c> is fired.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// When the <paramref name="service"/> instance doesn't support the event
    /// callback, this error is thrown.
    /// </exception>
    public static void OnVoicesChanged(
        this ISpeechSynthesisService service,
        Func<Task> onVoicesChanged)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } } svc)
        {
            var key = Guid.NewGuid();
            s_callbackRegistry[key] = onVoicesChanged;
            svc._javaScript.InvokeVoid(
                "blazorators.speechSynthesis.onVoicesChanged",
                "Blazor.SpeechSynthesis.WebAssembly",
                nameof(VoicesChangedAsync),
                key);
        }
        else
        {
            throw new InvalidOperationException(
                "SpeechSynthesisService is not available.");
        }
    }

    /// <summary>
    /// The <see cref="JSInvokableAttribute"/> callback.
    /// </summary>
    /// <param name="guid">The identifier that flows through the interop pipeline.</param>
    [JSInvokable]
    public static async Task VoicesChangedAsync(string guid)
    {
        if (Guid.TryParse(guid, out var key) &&
            s_callbackRegistry.TryRemove(key, out var callback))
        {
            await callback.Invoke().ConfigureAwait(false);
        }
    }
}
