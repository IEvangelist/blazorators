// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoInteropAttribute = @"#nullable enable
/// <summary>
/// Use this attribute on a <c>public partial interface</c> declaration. The
/// source generator emits the interface body, a concrete implementation, and
/// a <c>ServiceCollectionExtensions</c> class that registers both with DI.
/// For example:
/// <code>
/// [JSAutoInterop(
///    TypeName = ""Geolocation"",
///    Implementation = ""window.navigator.geolocation"",
///    HostingModel = BlazorHostingModel.WebAssembly,
///    Url = ""https://developer.mozilla.org/docs/Web/API/Geolocation"")]
/// public partial interface IGeolocationService;
/// </code>
/// This generates Blazor JavaScript-interop extensions for the <c>geolocation</c> APIs.
/// </summary>
[AttributeUsage(
    AttributeTargets.Interface,
    Inherited = false,
    AllowMultiple = false)]
public class JSAutoInteropAttribute : Attribute
{
    /// <summary>
    /// The type name that corresponds to the lib.dom.d.ts interface. For example, <c>""Geolocation""</c>.
    /// For more information, search 'interface {Name}'
    /// <a href='https://raw.githubusercontent.com/microsoft/TypeScript/main/lib/lib.dom.d.ts'>here for types</a>.
    /// </summary>
    public string TypeName { get; set; } = null!;

    /// <summary>
    /// The path from the <c>window</c> object to the corresponding <see cref=""TypeName""/> implementation.
    /// For example, if the <see cref=""TypeName""/> was <c>""Geolocation""</c>, this would be
    /// <c>""window.navigator.geolocation""</c> (or <c>""navigator.geolocation""</c>).
    /// </summary>
    public string Implementation { get; set; } = null!;

    /// <summary>
    /// Whether to generate only pure JavaScript functions that do not require callbacks.
    /// For example, <c>""Geolocation.clearWatch""</c> is consider pure, but <c>""Geolocation.watchPosition""</c> is not.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool OnlyGeneratePureJS { get; set; } = false;

    /// <summary>
    /// The Blazor hosting model to generate source for. WebAssembly creates <c>IJSInProcessRuntime</c> extensions,
    /// while Server creates <c>IJSRuntime</c> extensions. Defaults to <see cref=""BlazorHostingModel.WebAssembly"" />.
    /// </summary>
    public BlazorHostingModel HostingModel { get; set; } = BlazorHostingModel.WebAssembly;

    /// <summary>
    /// The optional URL to the corresponding API.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional array of additional TypeScript declaration source identifiers
    /// (file basenames, full paths, or trailing-path segments matching
    /// <c>AdditionalFiles</c> entries ending in <c>.d.ts</c>). When non-empty,
    /// the generator parses the matched <c>AdditionalFile</c> contents in
    /// place of the bundled <c>lib.dom.d.ts</c>. The consumer is responsible
    /// for adding the file(s) to the project, e.g.:
    /// <code>
    /// &lt;ItemGroup&gt;
    ///   &lt;AdditionalFiles Include=""decls\my-api.d.ts"" /&gt;
    /// &lt;/ItemGroup&gt;
    /// </code>
    /// When null or empty, the bundled <c>lib.dom.d.ts</c> parser is used.
    /// </summary>
    public string[]? TypeDeclarationSources { get; set; }
}
";
}