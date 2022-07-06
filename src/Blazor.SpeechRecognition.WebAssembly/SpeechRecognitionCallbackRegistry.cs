// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class SpeechRecognitionCallbackRegistry
{
    readonly ConcurrentDictionary<Guid, Action<string>> _onResultCallbackRegister = new();
    readonly ConcurrentDictionary<Guid, Action<SpeechRecognitionErrorEvent>> _onErrorCallbackRegister = new();
    readonly ConcurrentDictionary<Guid, Action> _onStartedCallbackRegister = new();
    readonly ConcurrentDictionary<Guid, Action> _onEndedCallbackRegister = new();

    internal void RegisterOnRecognized(
        Guid key, Action<string> callback) =>
        _onResultCallbackRegister[key] = callback;

    internal void RegisterOnError(
        Guid key, Action<SpeechRecognitionErrorEvent> callback) =>
        _onErrorCallbackRegister[key] = callback;

    internal void RegisterOnStarted(
        Guid key, Action callback) =>
        _onStartedCallbackRegister[key] = callback;

    internal void RegisterOnEnded(
        Guid key, Action callback) =>
        _onEndedCallbackRegister[key] = callback;

    internal void InvokeOnRecognized(
        string key, string transcript) =>
        OnInvokeCallback(
            key, _onResultCallbackRegister,
            callback => callback?.Invoke(transcript),
            remove: false);

    internal void InvokeOnError(
        string key, SpeechRecognitionErrorEvent error) =>
        OnInvokeCallback(
            key, _onErrorCallbackRegister,
            callback => callback?.Invoke(error));

    internal void InvokeOnStarted(string key) =>
        OnInvokeCallback(
            key, _onStartedCallbackRegister,
            callback => callback?.Invoke());

    internal void InvokeOnEnded(string key) =>
        OnInvokeCallback(
            key, _onEndedCallbackRegister,
            callback => callback?.Invoke());

    static void OnInvokeCallback<T>(
        string key,
        ConcurrentDictionary<Guid, T> callbackRegister,
        Action<T?> handleCallback,
        bool remove = true)
    {
        if (key is null or { Length: 0 } ||
            callbackRegister is null or { Count: 0 })
        {
            return;
        }

        if (Guid.TryParse(key, out var guid))
        {
            if (remove && callbackRegister.TryRemove(guid, out var callback))
            {
                handleCallback?.Invoke(callback);
            }
            else if (!remove && callbackRegister.TryGetValue(guid, out callback))
            {
                handleCallback?.Invoke(callback);
            }
        }
    }
}
