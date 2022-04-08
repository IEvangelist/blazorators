// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Humanizer;

namespace Blazor.ExampleConsumer.Pages;

public sealed partial class ReadToMe : IDisposable
{
    const string PreferredVoiceKey = "preferred-voice";
    const string PreferredSpeedKey = "preferred-speed";
    const string TextKey = "read-to-me-text";

    string? _text = "Blazorators is an open-source project that strives to simplify JavaScript interop in Blazor. JavaScript interoperability is possible by parsing TypeScript type declarations and using this metadata to output corresponding C# types.";
    SpeechSynthesisVoice[] _voices = Array.Empty<SpeechSynthesisVoice>();
    readonly IList<double> _voiceSpeeds =
        Enumerable.Range(0, 12).Select(i => (i + 1) * .25).ToList();
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

    [Inject]
    public ISpeechSynthesisService SpeechSynthesis { get; set; } = null!;

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = null!;

    [Inject]
    public ISessionStorageService SessionStorage { get; set; } = null!;

    [Inject]
    public ILogger<ReadToMe> Logger { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await GetVoicesAsync();

        SpeechSynthesis.OnVoicesChanged(
            async () => await GetVoicesAsync());

        if (LocalStorage.GetItem<string>(PreferredVoiceKey)
            is { Length: > 0 } voice)
        {
            _selectedVoice = voice;
        }
        if (LocalStorage.GetItem<double>(PreferredSpeedKey)
            is double speed && speed > 0)
        {
            _voiceSpeed = speed;
        }
        if (SessionStorage.GetItem<string>(TextKey)
            is { Length: > 0} text)
        {
            _text = text;
        }
    }

    async Task GetVoicesAsync()
    {
        _voices = await SpeechSynthesis.GetVoicesAsync();

        StateHasChanged();
    }

    void OnTextChanged(ChangeEventArgs args) => _text = args.Value?.ToString();

    void OnVoiceSpeedChange(ChangeEventArgs args) =>
        _voiceSpeed = double.TryParse(args.Value?.ToString() ?? "1.5", out var speed)
            ? speed : 1.5;

    void Speak() => SpeechSynthesis.Speak(
        Utterance,
        elapsedTime =>
        {
            _elapsedTimeMessage =
                $"Read aloud in {TimeSpan.FromMilliseconds(elapsedTime).Humanize()}.";

            StateHasChanged();
        });

    public void Dispose()
    {
        LocalStorage.SetItem(PreferredVoiceKey, _selectedVoice);
        LocalStorage.SetItem(PreferredSpeedKey, _voiceSpeed);
        SessionStorage.SetItem(TextKey, _text);

        SpeechSynthesis.UnsubscribeFromVoicesChanged();
    }
}
