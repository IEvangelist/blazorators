// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace BlazorServer.ExampleConsumer.Pages;

public sealed partial class ReadToMe : IAsyncDisposable
{
    const string PreferredVoiceKey = "preferred-voice";
    const string PreferredSpeedKey = "preferred-speed";
    const string TextKey = "read-to-me-text";

    string? _text = "Blazorators is an open-source project that strives to simplify JavaScript interop in Blazor. JavaScript interoperability is possible by parsing TypeScript type declarations and using this metadata to output corresponding C# types.";
    SpeechSynthesisVoice[] _voices = [];
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
        {
            return;
        }

        await GetVoicesAsync();

        if (await LocalStorage.GetItemAsync(PreferredVoiceKey)
            is { Length: > 0 } voice)
        {
            _selectedVoice = voice;
        }
        if (await LocalStorage.GetItemAsync(PreferredSpeedKey)
            is { Length: > 0 } s &&
            double.TryParse(s, out var speed) && speed > 0)
        {
            _voiceSpeed = speed;
        }
        if (await SessionStorage.GetItemAsync(TextKey)
            is { Length: > 0 } text)
        {
            _text = text;
        }
    }

    async Task GetVoicesAsync(bool isFromCallback = false)
    {
        _voices = await SpeechSynthesis.GetVoicesAsync();
        if (_voices is { } && isFromCallback)
        {
            StateHasChanged();
        }
    }

    void OnTextChanged(ChangeEventArgs args) => _text = args.Value?.ToString();

    void OnVoiceSpeedChange(ChangeEventArgs args) =>
        _voiceSpeed = double.TryParse(args.Value?.ToString() ?? "1.5", out var speed)
            ? speed : 1.5;

    ValueTask Speak() => SpeechSynthesis.SpeakAsync(Utterance);

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await LocalStorage.SetItemAsync(PreferredVoiceKey, _selectedVoice!);
        await LocalStorage.SetItemAsync(PreferredSpeedKey, _voiceSpeed.ToString());
        await SessionStorage.SetItemAsync(TextKey, _text!);
    }
}
