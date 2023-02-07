// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoInteropAttribute = @"#nullable enable
/// <summary>
/// Use this attribute on <code>public static partial</code> extension method classes.
/// For example:
/// <code>
/// [JSAutoInterop(
///    TypeName = ""Storage"",
///    Implementation = ""window.localStorage"",
///    HostingModel = BlazorHostingModel.WebAssembly,
///    Url = ""https://developer.mozilla.org/docs/Web/API/Window/localStorage"")]
/// public static partial LocalStorageExtensions
/// {
/// }
/// </code>
/// This will source generate all the extension methods for the IJSInProcessRuntime type for the localStorage APIs.
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
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool OnlyGeneratePureJS { get; set; } = true;

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
    /// An optional array of TypeScript type declarations sources. Valid values are URLs or file paths.
    /// </summary>
    public string[]? TypeDeclarationSources { get; set; }
}
";
}