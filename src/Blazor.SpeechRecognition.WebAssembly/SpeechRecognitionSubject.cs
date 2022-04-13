// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class SpeechRecognitionSubject : IDisposable
{
    readonly Subject<SpeechRecognitionResult> _speechRecognitionSubject = new();
    readonly IObservable<(string, string)> _speechRecognitionObservable;
    readonly IDisposable _speechRecognitionSubscription;
    readonly Action<string, string> _observer;

    private SpeechRecognitionSubject(
        Action<string, string> observer)
    {
        _observer = observer;
        _speechRecognitionObservable =
            _speechRecognitionSubject.AsObservable()
                .Where(recognition => recognition.IsFinal)
                .Select(recognition => (recognition.Key, recognition.Transcript));

        _speechRecognitionSubscription =
            _speechRecognitionObservable.Subscribe(
                ((string Key, string SpeechRecognition) tuple) =>
                    _observer(tuple.Key, tuple.SpeechRecognition));
    }

    internal static SpeechRecognitionSubject Factory(
        Action<string, string> observer) => new(observer);

    internal void RecognitionReceived(
        SpeechRecognitionResult recognition) =>
        _speechRecognitionSubject.OnNext(recognition);

    public void Dispose() => _speechRecognitionSubscription.Dispose();
}
