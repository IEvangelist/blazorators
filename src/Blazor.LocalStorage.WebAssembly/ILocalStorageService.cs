// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoGenericInterop(
    TypeName = "Storage",
    Implementation = "window.localStorage",
    Url = "https://developer.mozilla.org/docs/Web/API/Window/localStorage",
    GenericMethodDescriptors =
    [
        "getItem",
        "setItem:value"
    ])]
public partial interface ILocalStorageService;