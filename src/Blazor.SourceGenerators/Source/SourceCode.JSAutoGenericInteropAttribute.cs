// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoGenericInteropAttribute = @"#nullable enable
/// <summary>
/// Use this attribute on a <c>public partial interface</c> declaration when
/// the interop should expose generic <c>TValue</c> overloads (typically for
/// JSON-serialized payloads). The source generator emits the interface body,
/// a concrete implementation, and a <c>ServiceCollectionExtensions</c> class
/// that registers both with DI. For example:
/// <code>
/// [JSAutoGenericInterop(
///    TypeName = ""Storage"",
///    Implementation = ""window.localStorage"",
///    HostingModel = BlazorHostingModel.WebAssembly,
///    Url = ""https://developer.mozilla.org/docs/Web/API/Window/localStorage"",
///    GenericMethodDescriptors = new[]
///    {
///        ""getItem"",
///        ""setItem:value""
///    })]
/// public partial interface ILocalStorageService;
/// </code>
/// This generates the strongly-typed extensions for the <c>localStorage</c> APIs.
/// </summary>
public class JSAutoGenericInteropAttribute : JSAutoInteropAttribute
{
    /// <summary>
    /// The descriptors that define which APIs are to use default JSON serialization and support generics.
    /// For example:
    /// <code>
    /// new[]
    /// {
    ///     ""getItem"",      // Serializes the return type of getItem as TValue
    ///     ""setItem:value"" // Serializes the value parameter of the setItem TValue
    /// }
    /// </code>
    /// </summary>
    public string[] GenericMethodDescriptors { get; set; } = null!;

    /// <summary>
    /// The overrides that define which APIs to override (only applicable for pure JavaScript).
    /// For example:
    /// <code>
    /// new[]
    /// {
    ///     ""getVoices"",  // A pure JS method with by this name will have a custom impl.
    /// }
    /// </code>
    /// </summary>
    public string[] PureJavaScriptOverrides { get; set; } = null!;
}
";
}