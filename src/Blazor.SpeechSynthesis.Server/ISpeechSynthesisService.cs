// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// </summary>
[JSAutoInterop(
    TypeName = "SpeechSynthesis",
    Implementation = "window.speechSynthesis",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesis")]
public partial interface ISpeechSynthesisService
{
}