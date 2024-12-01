// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary></summary>
[JSAutoGenericInterop(
    TypeName = "Storage",
    Implementation = "window.sessionStorage",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/docs/Web/API/Window/sessionStorage",
    GenericMethodDescriptors =
[
        "getItem",
        "setItem:value"
    ])]
public partial interface ISessionStorageService;