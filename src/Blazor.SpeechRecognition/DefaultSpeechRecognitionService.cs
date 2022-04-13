// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DefaultSpeechRecognitionService : ISpeechRecognitionService
{
    readonly ConcurrentDictionary<Guid, Func<Task>> _onStartedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<Task>> _onEndedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<SpeechRecognitionErrorEvent, Task>> _onErrorCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<string, Task>> _onResultCallbackRegistry = new();
    readonly Lazy<Task<IJSObjectReference>> _speechRecognitionModule;
    SpeechRecognitionSubject? _speechRecognition;

    public DefaultSpeechRecognitionService(
        IJSRuntime javaScript) =>
        _speechRecognitionModule =
            new(() => javaScript.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Blazor.SpeechRecognition.WebAssembly/blazorators.speechRecognition.js")
                .AsTask());

    async ValueTask InitializeSpeechRecognitionSubjectAsync()
    {
        if (_speechRecognition is not null)
        {
            await CancelSpeechRecognitionAsync(false);
            _speechRecognition.Dispose();
        }

        _speechRecognition = SpeechRecognitionSubject.Factory(
            async (key, speechRecognition) =>
            {
                if (Guid.TryParse(key, out var guid) &&
                    _onResultCallbackRegistry.TryGetValue(guid, out var onRecognized))
                {
                    await onRecognized.Invoke(speechRecognition)
                        .ConfigureAwait(false);
                }
            });
    }

    /// <inheritdoc />
    public async Task CancelSpeechRecognitionAsync(bool isAborted)
    {
        var module = await _speechRecognitionModule.Value;
        if (module is not null)
        {
            await module.InvokeVoidAsync(
                JavaScriptInteropMethodIdentifiers.CancelSpeechRecognition,
                isAborted);
        }
    }

    /// <inheritdoc />
    public async Task<IDisposable> RecognizeSpeechAsync(
        string language,
        Func<string, Task> onRecognized,
        Func<SpeechRecognitionErrorEvent, Task>? onError,
        Func<Task>? onStarted,
        Func<Task>? onEnded)
    {
        var module = await _speechRecognitionModule.Value;
        if (module is not null)
        {
            await InitializeSpeechRecognitionSubjectAsync();

            var key = Guid.NewGuid();

            if (onStarted is not null) _onStartedCallbackRegistry[key] = onStarted;
            if (onEnded is not null) _onEndedCallbackRegistry[key] = onEnded;
            if (onError is not null) _onErrorCallbackRegistry[key] = onError;

            _onResultCallbackRegistry.Clear();
            _onResultCallbackRegistry[key] = onRecognized;
        
            await module.InvokeVoidAsync(
                JavaScriptInteropMethodIdentifiers.RecognizeSpeech,
                DotNetObjectReference.Create(this),
                language,
                key,
                nameof(OnSpeechRecongizedAsync),
                nameof(OnRecognitionErrorAsync),
                nameof(OnStartedAsync),
                nameof(OnEndedAsync));
        }

        return _speechRecognition!;
    }

    [JSInvokable]
    public Task OnStartedAsync(string key) =>
        OnInvokeCallbackAsync(
            key, _onStartedCallbackRegistry,
            async callback =>
                await callback.Invoke().ConfigureAwait(false));

    [JSInvokable]
    public Task OnEndedAsync(string key) =>
        OnInvokeCallbackAsync(
            key, _onEndedCallbackRegistry,
            async callback =>
                await callback.Invoke().ConfigureAwait(false));

    [JSInvokable]
    public Task OnRecognitionErrorAsync(string key, SpeechRecognitionErrorEvent errorEvent) =>
        OnInvokeCallbackAsync(
            key, _onErrorCallbackRegistry,
            async callback =>
                await callback.Invoke(errorEvent).ConfigureAwait(false));

    [JSInvokable]
    public void OnSpeechRecongizedAsync(string key, string transcript, bool isFinal) =>
        _speechRecognition?.RecognitionReceived(
            new SpeechRecognitionResult(key, transcript, isFinal));

    static Task OnInvokeCallbackAsync<T>(
        string key,
        ConcurrentDictionary<Guid, T> callbackRegistry,
        Func<T, Task> handleCallback)
    {
        if (key is null or { Length: 0 })
        {
            return Task.CompletedTask;
        }

        if (callbackRegistry is null or { Count: 0 })
        {
            return Task.CompletedTask;
        }

        if (handleCallback is not null &&
            Guid.TryParse(key, out var guid) &&
            callbackRegistry.TryRemove(guid, out var callback))
        {
            return handleCallback.Invoke(callback);
        }

        return Task.CompletedTask;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _speechRecognition?.Dispose();
        _speechRecognition = null;

        if (_speechRecognitionModule.IsValueCreated)
        {
            var module = await _speechRecognitionModule.Value;
            await module.DisposeAsync();
        }
    }
}
