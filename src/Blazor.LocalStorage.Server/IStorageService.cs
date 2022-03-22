// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Storage",
    Implementation = "window.localStorage",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/docs/Web/API/Window/localStorage")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial interface IStorageService
#pragma warning restore CS1591 // The XML comments are source generated
{
}