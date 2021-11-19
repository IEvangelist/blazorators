// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Storage",
    PathFromWindow = "localStorage",
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage")]
public static partial class LocalStorageExtensions
{
}
