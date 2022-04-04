// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary></summary>
[JSAutoInterop(
    TypeName = "Storage",
    Implementation = "window.sessionStorage",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/docs/Web/API/Window/sessionStorage")]
public partial interface ISessionStorageService
{
}