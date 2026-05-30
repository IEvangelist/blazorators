// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoServiceAttribute = @"#nullable enable
/// <summary>
/// Assembly-level attribute that fans out into the same source-generated
/// artifacts <see cref=""JSAutoInteropAttribute""/> produces, without
/// requiring a per-type <c>partial interface</c> anchor in the consumer's
/// code. For example:
/// <code>
/// [assembly: JSAutoService(""Geolocation"")]
/// </code>
/// generates <c>IGeolocationService</c>, <c>GeolocationService</c>, and a
/// <c>GeolocationServiceCollectionExtensions</c> class - same as writing
/// <c>[JSAutoInterop(TypeName=""Geolocation"", Implementation=""window.geolocation"")]</c>
/// on a <c>public partial interface IGeolocationService</c>.
/// <para>
/// The attribute allows multiple type names in a single application:
/// <code>
/// [assembly: JSAutoService(""Geolocation"", ""Clipboard"", ""Storage"")]
/// </code>
/// and is also marked <c>AllowMultiple</c> so several
/// <c>[assembly: JSAutoService(...)]</c> attributes can coexist in the
/// same assembly. <see cref=""Implementation""/> is only honored when the
/// attribute is applied with exactly one type name; for batch use the
/// <c>window.{camelCase(typeName)}</c> default is applied to each entry.
/// </para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Assembly,
    Inherited = false,
    AllowMultiple = true)]
public sealed class JSAutoServiceAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with one or more TypeScript type names to
    /// project. Each name produces a triplet of generated artifacts:
    /// <c>I{TypeName}Service</c>, <c>{TypeName}Service</c>, and the matching
    /// <c>{TypeName}ServiceCollectionExtensions</c> DI helper.
    /// </summary>
    /// <param name=""typeNames"">One or more TypeScript type/interface names.</param>
    public JSAutoServiceAttribute(params string[] typeNames)
    {
        TypeNames = typeNames;
    }

    /// <summary>
    /// The list of TypeScript type names this attribute is projecting.
    /// </summary>
    public string[] TypeNames { get; }

    /// <summary>
    /// Optional override for the JavaScript path used to invoke the runtime
    /// API. Only honored when the attribute is applied with a single
    /// <see cref=""TypeNames""/> entry; the multi-name form falls back to the
    /// default <c>window.{camelCase(typeName)}</c> for each entry.
    /// </summary>
    public string? Implementation { get; set; }

    /// <summary>
    /// The Blazor hosting model to generate source for. WebAssembly creates
    /// <c>IJSInProcessRuntime</c> extensions, while Server creates
    /// <c>IJSRuntime</c> extensions. Defaults to
    /// <see cref=""BlazorHostingModel.WebAssembly""/>.
    /// </summary>
    public BlazorHostingModel HostingModel { get; set; } = BlazorHostingModel.WebAssembly;

    /// <summary>
    /// Optional namespace override for the generated artifacts. When unset
    /// the generator emits into <c>Microsoft.JSInterop</c>.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Optional array of additional TypeScript declaration source identifiers
    /// (file basenames, full paths, or trailing-path segments matching
    /// <c>AdditionalFiles</c> entries ending in <c>.d.ts</c>). When non-empty
    /// the generator parses the matched <c>AdditionalFile</c> contents in
    /// place of the bundled <c>lib.dom.d.ts</c>.
    /// </summary>
    public string[]? TypeDeclarationSources { get; set; }
}
";
}
