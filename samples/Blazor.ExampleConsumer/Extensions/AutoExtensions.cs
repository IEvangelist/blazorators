// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Extensions;

[JSAutoInterop(
    TypeName = "Geolocation",
    PathFromWindow = "navigator.geolocation",
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Geolocation")]
public static partial class GeolocationExtensions
{
}