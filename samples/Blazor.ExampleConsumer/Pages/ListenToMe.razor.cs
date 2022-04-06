// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Pages;

public sealed partial class ListenToMe : IAsyncDisposable
{
    IDisposable? _recognitionSubscription;
    bool _isRecognizingSpeech = false;
    SpeechRecognitionErrorEvent? _errorEvent;    
    string? _transcript;

    [Inject]
    public ISpeechRecognitionService SpeechRecognition { get; set; } = null!;

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
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (SpeechRecognition is not null)
        {
            await SpeechRecognition.DisposeAsync();
        }

        _recognitionSubscription?.Dispose();
    }
}
