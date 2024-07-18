// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Clipboard",
    Implementation = "window.navigator.clipboard",
    Url = "https://developer.mozilla.org/docs/Web/API/Clipboard")]
public partial interface IClipboardService;