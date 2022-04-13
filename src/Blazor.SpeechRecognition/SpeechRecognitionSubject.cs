// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class SpeechRecognitionSubject : IDisposable
{
    readonly Subject<SpeechRecognitionResult> _speechRecognitionSubject = new();
    readonly IObservable<(string, string)> _speechRecognitionObservable;
    readonly IDisposable _speechRecognitionSubscription;
    readonly Func<string, string, Task> _observer;

    private SpeechRecognitionSubject(
        Func<string, string, Task> observer)
    {
        _observer = observer;
        _speechRecognitionObservable =
            _speechRecognitionSubject.AsObservable()
                .Where(recognition => recognition.IsFinal)
                .Select(recognition => (recognition.Key, recognition.Transcript));

        _speechRecognitionSubscription =
        _speechRecognitionObservable.Select(
            ((string Key, string SpeechRecognition) tuple) =>
                Observable.FromAsync(
                    async () => await _observer(tuple.Key, tuple.SpeechRecognition)))
            .Merge(maxConcurrent: 3)
            .Subscribe();
    }

    internal static SpeechRecognitionSubject Factory(
        Func<string, string, Task> observer) => new(observer);

    internal void RecognitionReceived(
        SpeechRecognitionResult recognition) =>
        _speechRecognitionSubject.OnNext(recognition);

    public void Dispose() => _speechRecognitionSubscription.Dispose();
}
