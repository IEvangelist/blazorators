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
/// <param name="PureJavaScriptOverrides">Overrides pure JavaScript calls. A custom impl must exist.</param>
/// <param name="TypeDeclarationSources">An optional array of TypeScript type declarations sources. Valid values are URLs or file paths.</param>
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
    string[]? PureJavaScriptOverrides = null,
    string[]? TypeDeclarationSources = null,
    bool IsWebAssembly = true)
{
    ISet<TypeDeclarationParser>? _parsers;

    /// <summary>
    /// Get <see cref="GeneratorOptions"/> instance maps its
    /// <see cref="TypeDeclarationSources"/> into a set of parsers.
    /// When <see cref="TypeDeclarationSources"/> is null, or empty,
    /// the default lib.dom.d.ts parser is used.
    /// </summary>
    internal ISet<TypeDeclarationParser> Parsers
    {
        get
        {
            _parsers ??= new HashSet<TypeDeclarationParser>();

            foreach (var source in
                TypeDeclarationSources?.Select(TypeDeclarationReader.Factory)
                    ?.Select(reader => new TypeDeclarationParser(reader))
                    ?? new[] { TypeDeclarationParser.Default })
            {
                _parsers.Add(source);
            }

            return _parsers;
        }
    }
}
