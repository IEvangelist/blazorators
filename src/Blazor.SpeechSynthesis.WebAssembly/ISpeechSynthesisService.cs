// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoGenericInterop(
    TypeName = "SpeechSynthesis",
    Implementation = "window.speechSynthesis",
    Url = "https://developer.mozilla.org/docs/Web/API/SpeechSynthesis",
    OnlyGeneratePureJS = true,
    PureJavaScriptOverrides =
    [
        "getVoices"
    ])]
public partial interface ISpeechSynthesisService;