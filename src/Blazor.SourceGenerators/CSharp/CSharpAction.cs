// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// Represents a C# delegate based on a TypeScript callback interface:
/// <example>
/// For example:
/// <code>
/// interface PositionCallback {
///     (position: GeolocationPosition): void;
/// }
/// </code>
/// </example>
/// Would be represented as:
/// <list type="bullet">
/// <item><c>RawName</c>: <c>PositionCallback</c></item>
/// <item><c>RawReturnTypeName</c>: <c>void</c></item>
/// <item><c>ParameterDefinitions</c>: <code>new List&lt;CSharpObject&gt; { new(RawName: "position", RawTypeName: "GeolocationPosition") }</code></item>
/// </list>
/// </summary>
internal record CSharpAction(
    string RawName,
    string? RawReturnTypeName = "void",
    List<CSharpType>? ParameterDefinitions = null)
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
