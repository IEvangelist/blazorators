// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal record GeneratorOptions(
    /// <summary>
    /// The type name that corresponds to the lib.dom.d.ts interface. For example, <c>""Geolocation""</c>.
    /// </summary>
    string? TypeName = null,

    /// <summary>
    /// The path from the <c>window</c> object. For example,
    /// <c>""window.navigator.geolocation""</c> (or <c>""navigator.geolocation""</c>).
    /// </summary>
    string? PathFromWindow = null,

    /// <summary>
    /// Whether to generate only pure JavaScript functions that do not require callbacks.
    /// For example, <c>Geolocation.clearWatch</c> is consider pure, but <c>Geolocation.watchPosition</c> is not.
    /// </summary>
    bool OnlyGeneratePureJS = false,

    /// <summary>
    /// The optional URL to the corresponding API.
    /// </summary>
    string? Url = null);
