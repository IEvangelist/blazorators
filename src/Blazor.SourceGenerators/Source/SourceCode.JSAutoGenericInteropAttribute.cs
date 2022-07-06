// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoGenericInteropAttribute = @"#nullable enable
/// <summary>
/// Use this attribute on <code>public static partial</code> extension method classes.
/// For example:
/// <example>
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
/// public static partial LocalStorageExtensions
/// {
/// }
/// </code>
/// </example>
/// This will source generate all the extension methods for the IJSInProcessRuntime type for the localStorage APIs.
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