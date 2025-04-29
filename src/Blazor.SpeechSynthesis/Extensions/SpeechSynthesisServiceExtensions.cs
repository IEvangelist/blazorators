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
    readonly static ConcurrentDictionary<string, Func<double, Task>> s_utteranceEndedCallbackRegistry = new();

    /// <summary>
    /// This extension wraps the <see cref="ISpeechSynthesisService.SpeakAsync(SpeechSynthesisUtterance)" />
    /// functionality, and exposes the native <c>onend</c> callback. When the utterance is done being read back,
    /// the elapsed time in milliseconds is returned.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the <paramref name="service"/> instance doesn't support the event
    /// callback, this error is thrown.
    /// </exception>
    public static async Task SpeakAsync(
        this ISpeechSynthesisService service,
        SpeechSynthesisUtterance utterance,
        Func<double, Task> onUtteranceEnded)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } } _)
        {
            s_utteranceEndedCallbackRegistry[utterance.Text] = onUtteranceEnded;
            await service.SpeakAsync(utterance);
        }
        else
        {
            throw new InvalidOperationException(
                "SpeechSynthesisService is not available.");
        }
    }

    /// <summary>
    /// The callback that is invoked from the <c>blazorators.speechSynthesis.speak</c> when the utterance is read.
    /// </summary>
    /// <param name="text">The text from the utterance that was read.</param>
    /// <param name="elapsedTimeSpokenInMilliseconds">
    /// The elapsed time in milliseconds that it took to read the utterance.
    /// </param>
    [JSInvokable(nameof(OnUtteranceEndedAsync))]
    public static async Task OnUtteranceEndedAsync(
        string text, double elapsedTimeSpokenInMilliseconds)
    {
        if (s_utteranceEndedCallbackRegistry.TryRemove(
            text, out var callback))
        {
            await callback.Invoke(elapsedTimeSpokenInMilliseconds);
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
    public static async Task OnVoicesChangedAsync(
        this ISpeechSynthesisService service,
        Func<Task> onVoicesChanged)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } } svc)
        {
            var key = Guid.NewGuid();
            s_callbackRegistry[key] = onVoicesChanged;
            await svc._javaScript.InvokeVoidAsync(
                "blazorators.speechSynthesis.onVoicesChanged",
                "Blazor.SpeechSynthesis",
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
    [JSInvokable(nameof(VoicesChangedAsync))]
    public static async Task VoicesChangedAsync(string guid)
    {
        if (Guid.TryParse(guid, out var key) &&
            s_callbackRegistry.TryRemove(key, out var callback))
        {
            await callback.Invoke().ConfigureAwait(false);
        }
    }

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
    public static async Task UnsubscribeFromVoicesChangedAsync(
        this ISpeechSynthesisService service)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } } svc)
        {
            await svc._javaScript.InvokeVoidAsync(
                "blazorators.speechSynthesis.unsubscribeVoicesChanged");
        }
        else
        {
            throw new InvalidOperationException(
                "SpeechSynthesisService is not available.");
        }
    }
}
