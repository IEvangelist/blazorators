// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Pages;

public sealed partial class ReadToMe : IDisposable
{
    string? _text = "Blazorators is an open-source project that strives to simplify JavaScript interop in Blazor.";
    SpeechSynthesisVoice[] _voices = Array.Empty<SpeechSynthesisVoice>();
    readonly IList<double> _voiceSpeeds =
        Enumerable.Range(0, 12).Select(i => (i + 1) * .25).ToList();
    double _voiceSpeed = 1.5;
    string? _selectedVoice;

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
    public IStorageService LocalStorage { get; set; } = null!;

    [Inject]
    public NavigationManager Nav { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshVoicesAsync();
        
        if (LocalStorage.GetItem<string>("preferred-voice")
            is { Length: > 0 } voice)
        {
            _selectedVoice = voice;
        }
        if (LocalStorage.GetItem<double>("preferred-speed")
            is double speed && speed > 0)
        {
            _voiceSpeed = speed;
        }
    }

    async Task RefreshVoicesAsync() => _voices = await SpeechSynthesis.GetVoicesAsync();

    void OnTextChanged(ChangeEventArgs args) => _text = args.Value?.ToString();

    void OnVoiceSpeedChange(ChangeEventArgs args) =>
        _voiceSpeed = double.TryParse(args.Value?.ToString() ?? "1.5", out var speed)
            ? speed : 1.5;

    void Speak() => SpeechSynthesis.Speak(Utterance);

    public void Dispose()
    {
        LocalStorage.SetItem("preferred-voice", _selectedVoice);
        LocalStorage.SetItem("preferred-speed", _voiceSpeed);
    }
}
