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
///    PathFromWindow = ""window.localStorage"",
///    HostingModel = BlazorHostingModel.WebAssembly,
///    Url = ""https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage"",
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
    ///     ""getItem"",      // Serializes the return type of getItem as TResult
    ///     ""setItem:value"" // Serializes the value parameter of the setItem TValue
    /// }
    /// </code>
    /// </summary>
    public string[] GenericMethodDescriptors { get; set; } = null!;
}
";
}