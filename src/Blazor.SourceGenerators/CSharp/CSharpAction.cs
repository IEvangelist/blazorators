// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// Represents a C# delegate based on a TypeScript callback interface.
/// </summary>
/// <example>
/// For example:
/// <code>
/// interface PositionCallback {
///     (position: GeolocationPosition): void;
/// }
/// </code>
/// Would be represented as:
/// <list type="bullet">
/// <item><c>RawName</c>: <c>PositionCallback</c></item>
/// <item><c>RawReturnTypeName</c>: <c>void</c></item>
/// <item><c>ParameterDefinitions</c>: <code>new List&lt;CSharpType&gt; { new("position", "GeolocationPosition") }</code></item>
/// </list>
/// </example>
internal record CSharpAction(
    string RawName,
    string? RawReturnTypeName = "void",
    IList<CSharpType>? ParameterDefinitions = null) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all dependent types of this C# action.
    /// </summary>
    public IImmutableSet<DependentType> AllDependentTypes => DependentTypes
        .Select(kvp => new DependentType(kvp.Key, kvp.Value))
        .Concat(ParameterDefinitions?.SelectMany(parameter => parameter.AllDependentTypes) ?? [])
        .ToImmutableHashSet(DependentTypeComparer.Default);
}