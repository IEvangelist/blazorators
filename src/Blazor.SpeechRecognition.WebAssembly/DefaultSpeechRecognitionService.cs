// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DefaultSpeechRecognitionService : ISpeechRecognitionService
{
    readonly IJSInProcessRuntime _javaScript;
    
    readonly ConcurrentDictionary<Guid, Action> _onStartedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action> _onEndedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action<SpeechRecognitionErrorEvent>> _onErrorCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action<string>> _onResultCallbackRegistry = new();

    SpeechRecognitionSubject? _speechRecognition;
    IJSInProcessObjectReference? _speechRecognitionModule;
    
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
            (key, speechRecognition) =>
            {
                if (Guid.TryParse(key, out var guid) &&
                    _onResultCallbackRegistry.TryGetValue(guid, out var onRecognized))
                {
                    onRecognized?.Invoke(speechRecognition);
                }
            });
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

        if (onStarted is not null) _onStartedCallbackRegistry[key] = onStarted;
        if (onEnded is not null) _onEndedCallbackRegistry[key] = onEnded;
        if (onError is not null) _onErrorCallbackRegistry[key] = onError;

        _onResultCallbackRegistry.Clear();
        _onResultCallbackRegistry[key] = onRecognized;

        _speechRecognitionModule?.InvokeVoid(
            InteropMethodIdentifiers.JavaScript.RecognizeSpeech,
            DotNetObjectReference.Create(this),
            language,
            key,
            nameof(OnSpeechRecongized),
            nameof(OnRecognitionError),
            nameof(OnStarted),
            nameof(OnEnded));

        return _speechRecognition!;
    }

    [JSInvokable]
    public void OnStarted(string key) =>
        OnInvokeCallback(
            key, _onStartedCallbackRegistry,
            callback => callback?.Invoke());

    [JSInvokable]
    public void OnEnded(string key) =>
        OnInvokeCallback(
            key, _onEndedCallbackRegistry,
            callback => callback?.Invoke());

    [JSInvokable]
    public void OnRecognitionError(string key, SpeechRecognitionErrorEvent errorEvent) =>
        OnInvokeCallback(
            key, _onErrorCallbackRegistry,
            callback => callback?.Invoke(errorEvent));

    [JSInvokable]
    public void OnSpeechRecongized(string key, string transcript, bool isFinal) =>
        _speechRecognition?.RecognitionReceived(
            new SpeechRecognitionResult(key, transcript, isFinal));

    static void OnInvokeCallback<T>(
        string key,
        ConcurrentDictionary<Guid, T> callbackRegistry,
        Action<T?> action)
    {
        if (key is null or { Length: 0 })
        {
            return;
        }

        if (callbackRegistry is null or { Count: 0 })
        {
            return;
        }

        if (Guid.TryParse(key, out var guid) &&
            callbackRegistry.TryRemove(guid, out var callback))
        {
            action?.Invoke(callback);
        }
    }

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
