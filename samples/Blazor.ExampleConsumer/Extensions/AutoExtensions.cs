// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Geolocation",
    PathFromWindow = "navigator.geolocation",
    HostingModel = BlazorHostingModel.Server,
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Geolocation")]
public static partial class GeolocationExtensions
{
}