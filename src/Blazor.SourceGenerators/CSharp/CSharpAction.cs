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
    IList<CSharpType>? ParameterDefinitions = null) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (var prop
                in DependentTypes.Select(
                        kvp => (TypeName: kvp.Key, Object: kvp.Value))
                    .Concat(ParameterDefinitions.SelectMany(
                        p => p.AllDependentTypes)))
            {
                result[prop.TypeName] = prop.Object;
            }

            return result.Select(kvp => (kvp.Key, kvp.Value))
                .ToImmutableHashSet();
        }
    }
}
