// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Geolocation",
    Implementation = "window.navigator.geolocation",
    Url = "https://developer.mozilla.org/docs/Web/API/Geolocation")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial interface IGeolocationService
#pragma warning restore CS1591 // The XML comments are source generated
{
}