// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class DefaultSpeechRecognitionService : ISpeechRecognitionService
{
    readonly ConcurrentDictionary<Guid, Func<Task>> _onStartedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<Task>> _onEndedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<SpeechRecognitionErrorEvent, Task>> _onErrorCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Func<string, Task>> _onResultCallbackRegistry = new();

    SpeechRecognitionSubject? _speechRecognition;
    Lazy<Task<IJSObjectReference>> _speechRecognitionModule;

    public DefaultSpeechRecognitionService(
        IJSRuntime javaScript) =>
        _speechRecognitionModule =
            new(() => javaScript.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Blazor.SpeechRecognition.WebAssembly/blazorators.speechRecognition.js").AsTask());

    async ValueTask InitializeSpeechRecognitionSubjectAsync()
    {
        if (_speechRecognition is not null)
        {
            if (this is ISpeechRecognitionService svc)
            {
                await svc.CancelSpeechRecognitionAsync(false);
            }

            _speechRecognition.Dispose();
        }

        _speechRecognition = SpeechRecognitionSubject.Create(
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
    async Task ISpeechRecognitionService.CancelSpeechRecognitionAsync(
        bool isAborted)
    {
        var module = await _speechRecognitionModule.Value;
        if (module is not null)
        {
            await module.InvokeVoidAsync(
                InteropMethodIdentifiers.JavaScript.CancelSpeechRecognition,
                isAborted);
        }
    }

    /// <inheritdoc />
    async Task ISpeechRecognitionService.RecognizeSpeechAsync<TComponent>(
        TComponent component,
        string language,
        string onResultCallbackMethodName,
        string? onStartCallbackMethodName,
        string? onEndCallbackMethodName,
        string? onErrorCallbackMethodName)
    {
        var module = await _speechRecognitionModule.Value;
        if (module is not null)
        {
            await module.InvokeVoidAsync(
                InteropMethodIdentifiers.JavaScript.RecognizeSpeech,
                DotNetObjectReference.Create<TComponent>(component),
                language,
                Guid.Empty,
                onResultCallbackMethodName,
                onErrorCallbackMethodName,
                onStartCallbackMethodName,
                onEndCallbackMethodName);
        }
    }

    /// <inheritdoc />
    async Task<IDisposable> ISpeechRecognitionService.RecognizeSpeechAsync(
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
                InteropMethodIdentifiers.JavaScript.RecognizeSpeech,
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
