// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Pages;

public partial class ReadToMe
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

    protected override async Task OnInitializedAsync() => await RefreshVoicesAsync();

    async Task RefreshVoicesAsync() => _voices = await SpeechSynthesis.GetVoicesAsync();

    void OnTextChanged(ChangeEventArgs args) => _text = args.Value?.ToString();

    void OnVoiceSpeedChange(ChangeEventArgs args) =>
        _voiceSpeed = double.TryParse(args.Value?.ToString() ?? "1.5", out var speed)
            ? speed : 1.5;

    void Speak() => SpeechSynthesis.Speak(Utterance);
}
