// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Pages;

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

    protected override void OnInitialized() =>
        _transcript = SessionStorage.GetItem<string>(TranscriptKey);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SpeechRecognition.InitializeModuleAsync();
        }
    }

    void OnRecognizeSpeechClick()
    {
        if (_isRecognizingSpeech)
        {
            SpeechRecognition.CancelSpeechRecognition(false);
        }
        else
        {
            var bcp47Tag = CurrentUICulture.Name;

            _recognitionSubscription?.Dispose();
            _recognitionSubscription = SpeechRecognition.RecognizeSpeech(
                bcp47Tag,
                OnRecognized,
                OnError,
                OnStarted,
                OnEnded);
        }
    }

    void OnStarted()
    {
        _isRecognizingSpeech = true;
        StateHasChanged();
    }

    void OnEnded()
    {
        _isRecognizingSpeech = false;
        StateHasChanged();
    }

    void OnError(SpeechRecognitionErrorEvent errorEvent)
    {
        _errorEvent = errorEvent;
        StateHasChanged();
    }

    void OnRecognized(string transcript)
    {
        _transcript = _transcript switch
        {
            null => transcript,
            _ => $"{_transcript.Trim()} {transcript}".Trim()
        };

        SessionStorage.SetItem(TranscriptKey, _transcript);
        StateHasChanged();
    }

    public void Dispose() => _recognitionSubscription?.Dispose();
}
