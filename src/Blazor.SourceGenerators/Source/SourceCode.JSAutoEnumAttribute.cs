// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string JSAutoEnumAttribute = @"#nullable enable
/// <summary>
/// Projects a TypeScript string-literal union (e.g.
/// <c>type DocumentReadyState = ""complete"" | ""interactive"" | ""loading"";</c>)
/// into a strongly-typed C# enum + a generated
/// <c>System.Text.Json.Serialization.JsonConverter&lt;TEnum&gt;</c> that maps
/// each enum value to its exact raw TypeScript string (and back) without
/// relying on <c>JsonStringEnumConverter</c>'s name-based heuristics.
/// <para>
/// Anchor this attribute on any C# interface in your project. The interface
/// itself is used purely as a discovery handle; the generator emits a
/// sibling <c>enum</c> (and its converter) next to the anchor, not into
/// the anchor's body, so the anchor does not have to be <c>partial</c>.
/// </para>
/// <code>
/// // Bare form (TypeName inferred from the interface name).
/// [JSAutoEnum]
/// public interface IDocumentReadyState { }
/// // -&gt; public enum DocumentReadyState { Complete, Interactive, Loading }
///
/// // Explicit form.
/// [JSAutoEnum(TypeName = ""InsertPosition"")]
/// public interface IInsertion { }
/// </code>
/// <para>
/// <see cref=""TypeName""/> defaults to the anchor interface name with a
/// leading <c>I</c> stripped (so <c>IDocumentReadyState</c> resolves to
/// the TypeScript <c>DocumentReadyState</c> alias). Supply an explicit
/// value to project a union whose name does not match the anchor.
/// </para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Interface,
    Inherited = false,
    AllowMultiple = false)]
public sealed class JSAutoEnumAttribute : Attribute
{
    /// <summary>
    /// Optional explicit name of the TypeScript string-literal union to
    /// project. When unset, the generator strips a leading <c>I</c> from
    /// the anchor interface name to derive the TypeScript alias name
    /// (<c>IDocumentReadyState</c> -&gt; <c>DocumentReadyState</c>).
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Optional override for the C# namespace the generated enum lands in.
    /// When unset, the enum is emitted into the anchor interface's
    /// namespace (or the global namespace if the anchor itself sits in the
    /// global namespace).
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Optional array of additional TypeScript declaration source
    /// identifiers (file basenames, full paths, or trailing-path segments
    /// matching <c>AdditionalFiles</c> entries ending in <c>.d.ts</c>).
    /// When non-empty the generator parses the matched <c>AdditionalFile</c>
    /// contents in place of the bundled <c>lib.dom.d.ts</c>.
    /// </summary>
    public string[]? TypeDeclarationSources { get; set; }
}
";
}
