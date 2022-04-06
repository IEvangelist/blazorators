// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// A service the exposes various JavaScript interop capabilities specific to the
/// <c>speechRecognition</c> APIs. See <a href="https://developer.mozilla.org/docs/Web/API/SpeechRecognition"></a>
/// </summary>
public interface ISpeechRecognitionService : IAsyncDisposable
{
    /// <summary>
    /// Cancels the active speech recognition session.
    /// </summary>
    /// <param name="isAborted">
    /// Is aborted controls which API to call,
    /// either <c>speechRecognition.stop</c> or <c>speechRecognition.abort</c>.
    /// </param>
    Task CancelSpeechRecognitionAsync(bool isAborted);

    /// <summary>
    /// Starts the speech recognition process. Returns an <see cref="IDisposable"/>
    /// that acts as the subscription. The various callbacks are invoked as they occur,
    /// and will continue to fire until the subscription is disposed of.
    /// </summary>
    /// <param name="language">The BCP47 language tag.</param>
    /// <param name="onRecognized">The callback to invoke when <c>onrecognized</c> fires.</param>
    /// <param name="onError">The optional callback to invoke when <c>onerror</c> fires.</param>
    /// <param name="onStarted">The optional callback to invoke when <c>onstarted</c> fires.</param>
    /// <param name="onEnded">The optional callback to invoke when <c>onended</c> fires.</param>
    /// <returns>
    /// To unsubscribe from the speech recognition, call
    /// <see cref="IDisposable.Dispose"/>.
    /// </returns>
    Task<IDisposable> RecognizeSpeechAsync(
        string language,
        Func<string, Task> onRecognized,
        Func<SpeechRecognitionErrorEvent, Task>? onError = null,
        Func<Task>? onStarted = null,
        Func<Task>? onEnded = null);
}
