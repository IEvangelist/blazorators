// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoGenericInterop(
    TypeName = "SpeechSynthesis",
    Implementation = "window.speechSynthesis",
    HostingModel = BlazorHostingModel.Server,
    Url = "https://developer.mozilla.org/docs/Web/API/SpeechSynthesis",
    OnlyGeneratePureJS = true,
    PureJavaScriptOverrides = new[]
    {
        "getVoices"
    })]
public partial interface ISpeechSynthesisService
{
}