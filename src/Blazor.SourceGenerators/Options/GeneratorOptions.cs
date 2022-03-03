// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

/// <summary>
/// The options used (and parsed from the <see cref="JSAutoInteropAttribute"/>) to source-generate JavaScript interop.
/// </summary>
/// <param name="TypeName">The type name that corresponds to the lib.dom.d.ts interface. For example, <c>"Geolocation"</c></param>
/// <param name="PathFromWindow">The path from the <c>window</c> object. For example, <c>"window.navigator.geolocation"</c> (or <c>"navigator.geolocation"</c>)</param>
/// <param name="OnlyGeneratePureJS">Whether to generate only pure JavaScript functions that do not require callbacks. For example, <c>Geolocation.clearWatch</c> is consider pure, but <c>Geolocation.watchPosition</c> is not.</param>
/// <param name="HostingModel">The Blazor hosting model to generate source for. WebAssembly creates <c>IJSInProcessRuntime</c> extensions, while Server creates <c>IJSRuntime</c> extensions. Defaults to <c>WebAssembly</c></param>
/// <param name="Url">The optional URL to the corresponding API.</param>
/// <param name="GenericMethodDescriptors">The optional generic method descriptors value from the parsed <see cref="JSAutoGenericInteropAttribute.GenericMethodDescriptors"/>.</param>
internal sealed record GeneratorOptions(
    string? TypeName = null,
    string? PathFromWindow = null,
    bool OnlyGeneratePureJS = false,
    BlazorHostingModel HostingModel = BlazorHostingModel.WebAssembly,
    string? Url = null,
    string[]? GenericMethodDescriptors = null)
{
    internal bool IsWebAssembly => HostingModel is BlazorHostingModel.WebAssembly;
}
