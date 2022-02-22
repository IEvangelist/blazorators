// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop.Extensions;

[JSAutoInterop(
    TypeName = "Storage",
    PathFromWindow = "window.localStorage",
    HostingModel = BlazorHostingModel.WebAssembly,
    Url = "https://developer.mozilla.org/docs/Web/API/Storage")]
public static partial class LocalStorageExtensions
{
}
