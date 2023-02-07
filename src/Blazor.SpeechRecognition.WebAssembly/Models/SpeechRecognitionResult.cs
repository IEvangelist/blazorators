// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// An object representing the result of speech recongition.
/// </summary>
/// <param name="Key">
/// A value that uniquely identifies the recognition result.
/// Is typically used to pair callbacks to callers.
/// </param>
/// <param name="Transcript">
/// The current text transcript from what was recognized as speech.
/// </param>
/// <param name="IsFinal">
/// Whether the <paramref name="Transcript"/> value is considered final.
/// When <c>true</c>, the <paramref name="Transcript"/> contains a
/// complete text representation of the recognized speech.
/// </param>
public record class SpeechRecognitionResult(
    string Key, string Transcript, bool IsFinal);