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
    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes =>
        DependentTypes
            .Select(kvp => (TypeName: kvp.Key, Object: kvp.Value))
            .Concat(ParameterDefinitions?.SelectMany(parameter => parameter.AllDependentTypes) ?? [])
            .GroupBy(kvp => kvp.TypeName)
            .Select(kvp => (TypeName: kvp.Key, kvp.Last().Object))
            .ToImmutableHashSet();

    /// <summary>
    /// Adds a dependent type to the collection.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="csharpObject">The C# object representing the type.</param>
    public void AddDependentType(string typeName, CSharpObject csharpObject) =>
        DependentTypes[typeName] = csharpObject;
}