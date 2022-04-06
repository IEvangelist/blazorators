// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DefaultSpeechRecognitionService : ISpeechRecognitionService
{
    readonly IJSInProcessRuntime _javaScript;
    readonly SpeechRecognitionCallbackRegistry _callbackRegistry = new();

    IJSInProcessObjectReference? _speechRecognitionModule;
    SpeechRecognitionSubject? _speechRecognition;

    public DefaultSpeechRecognitionService(
        IJSInProcessRuntime javaScript) => _javaScript = javaScript;

    void InitializeSpeechRecognitionSubject()
    {
        if (_speechRecognition is not null)
        {
            if (this is ISpeechRecognitionService svc)
            {
                svc.CancelSpeechRecognition(false);
            }

            _speechRecognition.Dispose();
        }

        _speechRecognition = SpeechRecognitionSubject.Create(
            _callbackRegistry.InvokeOnRecognized);
    }

    /// <inheritdoc />
    async Task ISpeechRecognitionService.InitializeModuleAsync() =>
        _speechRecognitionModule =
            await _javaScript.InvokeAsync<IJSInProcessObjectReference>(
                "import",
                "./_content/Blazor.SpeechRecognition.WebAssembly/blazorators.speechRecognition.js");

    /// <inheritdoc />
    void ISpeechRecognitionService.CancelSpeechRecognition(
        bool isAborted) =>
        _speechRecognitionModule?.InvokeVoid(
            InteropMethodIdentifiers.JavaScript.CancelSpeechRecognition,
            isAborted);

    /// <inheritdoc />
    IDisposable ISpeechRecognitionService.RecognizeSpeech(
        string language,
        Action<string> onRecognized,
        Action<SpeechRecognitionErrorEvent>? onError,
        Action? onStarted,
        Action? onEnded)
    {
        InitializeSpeechRecognitionSubject();

        var key = Guid.NewGuid();
        _callbackRegistry.RegisterOnRecognized(key, onRecognized);
        _callbackRegistry.RegisterOnError(key, onError);
        _callbackRegistry.RegisterOnStarted(key, onStarted);
        _callbackRegistry.RegisterOnEnded(key, onEnded);

        _speechRecognitionModule?.InvokeVoid(
            InteropMethodIdentifiers.JavaScript.RecognizeSpeech,
            DotNetObjectReference.Create(this),
            language,
            key,
            nameof(OnSpeechRecognized),
            nameof(OnRecognitionError),
            nameof(OnStarted),
            nameof(OnEnded));

        return _speechRecognition!;
    }

    [JSInvokable]
    public void OnStarted(string key) => _callbackRegistry.InvokeOnStarted(key);

    [JSInvokable]
    public void OnEnded(string key) => _callbackRegistry.InvokeOnEnded(key);

    [JSInvokable]
    public void OnRecognitionError(string key, SpeechRecognitionErrorEvent errorEvent) =>
        _callbackRegistry.InvokeOnError(key, errorEvent);

    [JSInvokable]
    public void OnSpeechRecognized(string key, string transcript, bool isFinal) =>
        _speechRecognition?.RecognitionReceived(
            new SpeechRecognitionResult(key, transcript, isFinal));

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _speechRecognition?.Dispose();
        _speechRecognition = null;

        if (_speechRecognitionModule is not null)
        {
            await _speechRecognitionModule.DisposeAsync();
            _speechRecognitionModule = null;
        }
    }
}
