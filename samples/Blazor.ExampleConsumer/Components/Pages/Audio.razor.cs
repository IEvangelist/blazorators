// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Components.Pages;

public sealed partial class Audio(
    ISpeechRecognitionService speechRecognition,
    ISpeechSynthesisService speechSynthesis,
    ILocalStorageService localStorage,
    ISessionStorageService sessionStorage) : IDisposable
{
    IDisposable? _recognitionSubscription;
    bool _isRecognizingSpeech = false;
    SpeechRecognitionErrorEvent? _errorEvent;
    string? _transcript;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await speechRecognition.InitializeModuleAsync();
        }
    }

    void OnRecognizeSpeechClick()
    {
        if (_isRecognizingSpeech)
        {
            speechRecognition.CancelSpeechRecognition(false);
        }
        else
        {
            var bcp47Tag = CurrentUICulture.Name;

            _recognitionSubscription?.Dispose();
            _recognitionSubscription = speechRecognition.RecognizeSpeech(
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

        StateHasChanged();
    }

    public void Dispose()
    {
        _recognitionSubscription?.Dispose();

        speechSynthesis.UnsubscribeFromVoicesChanged();
    }
}
