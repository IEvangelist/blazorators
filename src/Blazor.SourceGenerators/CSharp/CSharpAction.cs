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
    List<CSharpType>? ParameterDefinitions = null) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the C# generic argument list for this callback's
    /// <c>Action&lt;...&gt;</c> emission. Each parameter's
    /// <c>RawTypeName</c> is mapped through
    /// <see cref="TypeMap.PrimitiveTypes"/> (with array-of-primitive
    /// shapes peeled and re-attached), so a TS callback like
    /// <c>(time: number): void</c> emits <c>Action&lt;double&gt;</c>
    /// instead of the invalid <c>Action&lt;number&gt;</c>.
    ///
    /// <para>
    /// This is the single source of truth for the action's generic
    /// argument list; both <see cref="CSharpType.ToActionString"/> and
    /// <see cref="SourceBuilder.AppendConditionalDelegateFields"/> route
    /// through here. Previously the two emit paths derived their
    /// argument lists from different collections (the former from
    /// <see cref="DependentTypes"/> keys, which only contains custom
    /// types; the latter from <see cref="ParameterDefinitions"/>
    /// <c>RawTypeName</c> verbatim, which leaks raw TypeScript
    /// spellings) and disagreed for any callback whose signature
    /// mixed primitive and custom parameters.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> MappedActionTypeArguments
    {
        get
        {
            if (ParameterDefinitions is null || ParameterDefinitions.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(ParameterDefinitions.Count);
            foreach (var parameter in ParameterDefinitions)
            {
                result.Add(MapParameterTypeToCSharp(parameter.RawTypeName));
            }

            return result;
        }
    }

    internal static string MapParameterTypeToCSharp(string rawTypeName)
    {
        if (TypeMap.PrimitiveTypes.IsPrimitiveType(rawTypeName))
        {
            return TypeMap.PrimitiveTypes[rawTypeName];
        }

        if (TypeShape.TryGetArrayElementTypeName(rawTypeName, out var elementTypeName)
            && TypeMap.PrimitiveTypes.IsPrimitiveType(elementTypeName))
        {
            return $"{TypeMap.PrimitiveTypes[elementTypeName]}[]";
        }

        return rawTypeName;
    }

    private IImmutableSet<(string TypeName, CSharpObject Object)>? _allDependentTypes;
    private bool _isComputingAllDependentTypes;

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            if (_allDependentTypes is not null)
            {
                return _allDependentTypes;
            }

            if (_isComputingAllDependentTypes)
            {
                return ImmutableHashSet<(string, CSharpObject)>.Empty;
            }

            _isComputingAllDependentTypes = true;
            try
            {
                Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
                var parameterDependencies = ParameterDefinitions is null
                    ? Enumerable.Empty<(string TypeName, CSharpObject Object)>()
                    : ParameterDefinitions.SelectMany(p => p.AllDependentTypes);

                foreach (var prop
                    in DependentTypes.Select(
                            kvp => (TypeName: kvp.Key, Object: kvp.Value))
                        .Concat(parameterDependencies))
                {
                    result[prop.TypeName] = prop.Object;
                }

                return _allDependentTypes = result.Select(kvp => (kvp.Key, kvp.Value))
                    .ToImmutableHashSet();
            }
            finally
            {
                _isComputingAllDependentTypes = false;
            }
        }
    }
}
