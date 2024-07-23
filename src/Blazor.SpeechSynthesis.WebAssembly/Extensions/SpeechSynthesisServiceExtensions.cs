// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace Microsoft.JSInterop;

/// <summary>
/// Extension methods for the <see cref="ISpeechSynthesisService"/> functionality.
/// </summary>
public static class SpeechSynthesisServiceExtensions
{
    private static readonly ConcurrentDictionary<Guid, Func<Task>> s_callbackRegistry = new();
    private static readonly ConcurrentDictionary<string, Action<double>> s_utteranceEndedCallbackRegistry = new();

    /// <summary>
    /// This extension wraps the <see cref="ISpeechSynthesisService.Speak(SpeechSynthesisUtterance)" />
    /// functionality, and exposes the native <c>onend</c> callback. When the utterance is done being read back,
    /// the elapsed time in milliseconds is returned.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the <paramref name="service"/> instance doesn't support the event
    /// callback, this error is thrown.
    /// </exception>
    public static void Speak(
        this ISpeechSynthesisService service,
        SpeechSynthesisUtterance utterance,
        Action<double> onUtteranceEnded)
    {
        if (service is SpeechSynthesisService and { _javaScript: { } })
        {
            s_utteranceEndedCallbackRegistry[utterance.Text] = onUtteranceEnded;
            service.Speak(utterance);
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
    [JSInvokable(nameof(OnUtteranceEnded))]
    public static void OnUtteranceEnded(
        string text, double elapsedTimeSpokenInMilliseconds)
    {
        if (s_utteranceEndedCallbackRegistry.TryRemove(
            text, out var callback))
        {
            callback.Invoke(elapsedTimeSpokenInMilliseconds);
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
}