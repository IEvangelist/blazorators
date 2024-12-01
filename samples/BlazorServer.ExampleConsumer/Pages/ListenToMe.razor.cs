// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace BlazorServer.ExampleConsumer.Pages;

public sealed partial class ListenToMe : IDisposable
{
    const string TranscriptKey = "listen-to-me-page-transcript";

    IDisposable? _recognitionSubscription;
    bool _isRecognizingSpeech = false;
    SpeechRecognitionErrorEvent? _errorEvent;
    string? _transcript;

    [Inject]
    public ISpeechRecognitionService SpeechRecognition { get; set; } = null!;

    [Inject]
    public ISessionStorageService SessionStorage { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
        {
            return;
        }

        _transcript = await SessionStorage.GetItemAsync<string?>(TranscriptKey);
    }

    async Task OnRecognizeSpeechClick()
    {
        if (_isRecognizingSpeech)
        {
            await SpeechRecognition.CancelSpeechRecognitionAsync(false);
        }
        else
        {
            var bcp47Tag = CurrentUICulture.Name;

            _recognitionSubscription?.Dispose();
            _recognitionSubscription = await SpeechRecognition.RecognizeSpeechAsync(
                bcp47Tag,
                OnRecognized,
                OnError,
                OnStarted,
                OnEnded);
        }
    }

    Task OnStarted() =>
        InvokeAsync(() =>
        {
            _isRecognizingSpeech = true;
            StateHasChanged();
        });

    Task OnEnded() =>
        InvokeAsync(() =>
        {
            _isRecognizingSpeech = false;
            StateHasChanged();
        });

    Task OnError(SpeechRecognitionErrorEvent errorEvent) =>
        InvokeAsync(() =>
        {
            _errorEvent = errorEvent;
            StateHasChanged();
        });

    Task OnRecognized(string transcript) =>
        InvokeAsync(async () =>
        {
            _transcript = _transcript switch
            {
                null => transcript,
                _ => $"{_transcript.Trim()} {transcript}".Trim()
            };

            await SessionStorage.SetItemAsync(TranscriptKey, _transcript);
            StateHasChanged();
        });

    public void Dispose() => _recognitionSubscription?.Dispose();
}
