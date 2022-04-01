// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Storage",
    Implementation = "window.localStorage",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/docs/Web/API/Window/localStorage")]
public partial interface IStorageService
{
}