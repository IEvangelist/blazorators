// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Humanizer;

namespace Blazor.ExampleConsumer.Components.Pages;

public sealed partial class ReadToMe(
    ISpeechSynthesisService speechSynthesis,
    ILocalStorageService localStorage,
    ISessionStorageService sessionStorage) : IDisposable
{
    const string PreferredVoiceKey = "preferred-voice";
    const string PreferredSpeedKey = "preferred-speed";
    const string TextKey = "read-to-me-text";

    string? _text = "Blazorators is an open-source project that strives to simplify JavaScript interop in Blazor. JavaScript interoperability is possible by parsing TypeScript type declarations and using this metadata to output corresponding C# types.";
    SpeechSynthesisVoice[] _voices = [];
    readonly IList<double> _voiceSpeeds =
        [..Enumerable.Range(0, 12).Select(i => (i + 1) * .25)];
    double _voiceSpeed = 1.5;
    string? _selectedVoice;
    string? _elapsedTimeMessage = null;

    SpeechSynthesisUtterance Utterance => new()
    {
        Text = _text ?? "You forgot to try uttering some text.",
        Rate = _voiceSpeed,
        Volume = 1,
        Voice = _selectedVoice is { Length: > 0 }
            ? _voices?.FirstOrDefault(voice => voice.Name == _selectedVoice)
            : null
    };

    protected override async Task OnInitializedAsync()
    {
        await GetVoicesAsync();
        speechSynthesis.OnVoicesChanged(() => GetVoicesAsync(true));

        if (localStorage.GetItem<string>(PreferredVoiceKey)
            is { Length: > 0 } voice)
        {
            _selectedVoice = voice;
        }
        if (localStorage.GetItem<double>(PreferredSpeedKey)
            is double speed && speed > 0)
        {
            _voiceSpeed = speed;
        }
        if (sessionStorage.GetItem<string>(TextKey)
            is { Length: > 0 } text)
        {
            _text = text;
        }
    }

    async Task GetVoicesAsync(bool isFromCallback = false)
    {
        _voices = await speechSynthesis.GetVoicesAsync();
        if (_voices is { } && isFromCallback)
        {
            StateHasChanged();
        }
    }

    void OnTextChanged(ChangeEventArgs args) => _text = args.Value?.ToString();

    void OnVoiceSpeedChange(ChangeEventArgs args) =>
        _voiceSpeed = double.TryParse(args.Value?.ToString() ?? "1.5", out var speed)
            ? speed : 1.5;

    void Speak() => speechSynthesis.Speak(
        Utterance,
        elapsedTime =>
        {
            _elapsedTimeMessage =
                $"Read aloud in {TimeSpan.FromMilliseconds(elapsedTime).Humanize()}.";

            StateHasChanged();
        });

    void IDisposable.Dispose()
    {
        localStorage.SetItem(PreferredVoiceKey, _selectedVoice);
        localStorage.SetItem(PreferredSpeedKey, _voiceSpeed);
        sessionStorage.SetItem(TextKey, _text);

        speechSynthesis.UnsubscribeFromVoicesChanged();
    }
}
