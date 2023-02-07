// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// The speech recognition error and message pair.
/// </summary>
/// <param name="Error">The error's </param>
/// <param name="Message"></param>
public record class SpeechRecognitionErrorEvent(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message")] string Message);