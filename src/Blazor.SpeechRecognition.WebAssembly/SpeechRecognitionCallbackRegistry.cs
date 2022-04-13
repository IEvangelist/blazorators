// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class SpeechRecognitionCallbackRegistry
{
    readonly ConcurrentDictionary<Guid, Action<string>> _onResultCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action<SpeechRecognitionErrorEvent>> _onErrorCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action> _onStartedCallbackRegistry = new();
    readonly ConcurrentDictionary<Guid, Action> _onEndedCallbackRegistry = new();

    internal void RegisterOnRecognized(
        Guid key, Action<string> callback) => _onResultCallbackRegistry[key] = callback;

    internal void RegisterOnError(
        Guid key, Action<SpeechRecognitionErrorEvent>? callback)
    {
        if (callback is not null)
            _onErrorCallbackRegistry[key] = callback;
    }

    internal void RegisterOnStarted(
        Guid key, Action? callback)
    {
        if (callback is not null)
            _onStartedCallbackRegistry[key] = callback;
    }

    internal void RegisterOnEnded(
        Guid key, Action? callback)
    {
        if (callback is not null)
            _onEndedCallbackRegistry[key] = callback;
    }

    internal void InvokeOnRecognized(
        string key, string transcript) =>
        OnInvokeCallback(
            key, _onResultCallbackRegistry,
            callback => callback?.Invoke(transcript));

    internal void InvokeOnError(
        string key, SpeechRecognitionErrorEvent error) =>
        OnInvokeCallback(
            key, _onErrorCallbackRegistry,
            callback => callback?.Invoke(error));

    internal void InvokeOnStarted(string key) =>
        OnInvokeCallback(
            key, _onStartedCallbackRegistry,
            callback => callback?.Invoke());

    internal void InvokeOnEnded(string key) =>
        OnInvokeCallback(
            key, _onEndedCallbackRegistry,
            callback => callback?.Invoke());

    static void OnInvokeCallback<T>(
        string key,
        ConcurrentDictionary<Guid, T> callbackRegistry,
        Action<T?> handleCallback)
    {
        if (key is null or { Length: 0 } ||
            callbackRegistry is null or { Count: 0 })
        {
            return;
        }

        if (Guid.TryParse(key, out var guid) &&
            callbackRegistry.TryRemove(guid, out var callback))
        {
            handleCallback?.Invoke(callback);
        }
    }
}
