// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Geolocation",
    Implementation = "window.navigator.geolocation",
    HostingModel = BlazorHostingModel.Server,
    Url = "https://developer.mozilla.org/docs/Web/API/Geolocation")]
public partial interface IGeolocationService;