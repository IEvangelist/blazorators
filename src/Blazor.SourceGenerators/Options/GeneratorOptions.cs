// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

/// <summary>
/// The options used (and parsed from the <c>JSAutoInteropAttribute</c>) to source-generate JavaScript interop.
/// </summary>
/// <param name="SupportsGenerics">Indicates wether the generator supports generics.</param>
/// <param name="TypeName">The type name that corresponds to the lib.dom.d.ts interface. For example, <c>"Geolocation"</c></param>
/// <param name="Implementation">The path from the <c>window</c> object. For example, <c>"window.navigator.geolocation"</c> (or <c>"navigator.geolocation"</c>)</param>
/// <param name="OnlyGeneratePureJS">Whether to generate only pure JavaScript functions that do not require callbacks. For example, <c>Geolocation.clearWatch</c> is consider pure, but <c>Geolocation.watchPosition</c> is not.</param>
/// <param name="Url">The optional URL to the corresponding API.</param>
/// <param name="GenericMethodDescriptors">The optional generic method descriptors value from the parsed <c>JSAutoGenericInteropAttribute.GenericMethodDescriptors</c>.</param>
/// <param name="IsWebAssembly">
/// A value indicating whether to generate targeting WASM:
/// When <c>true</c>: Synchronous extensions are generated on the <c>IJSInProcessRuntime</c> type.
/// When <c>false</c>: Asynchronous extensions are generated on the <c>IJSRuntime</c> type.
/// </param>
internal sealed record GeneratorOptions(
    bool SupportsGenerics,
    string TypeName = null!,
    string Implementation = null!,
    bool OnlyGeneratePureJS = false,
    string? Url = null,
    string[]? GenericMethodDescriptors = null,
    bool IsWebAssembly = true);
