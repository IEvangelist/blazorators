// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Geolocation",
    Implementation = "window.navigator.geolocation",
    Url = "https://developer.mozilla.org/docs/Web/API/Geolocation")]
internal static partial class SynchronousGeolocationExtensions
{
}