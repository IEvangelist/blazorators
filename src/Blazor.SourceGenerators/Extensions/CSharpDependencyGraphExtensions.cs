// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpDependencyGraphExtensions
{
    internal static IImmutableSet<DependentType> GetAllDependencies(this ICSharpDependencyGraphObject dependencyGraphObject)
    {
        return (dependencyGraphObject.DependentTypes?.Flatten(obj => obj.Value.DependentTypes) ?? [])
            .Select(kvp => new DependentType(kvp.Key, kvp.Value))
            .ToImmutableHashSet(DependentTypeComparer.Default);
    }

    internal static IEnumerable<DependentType> GetDependentTypes(this IEnumerable<CSharpType>? parameterDefinitions)
    {
        if (parameterDefinitions == null) return [];
        return parameterDefinitions
            .SelectMany(pd => pd.DependentTypes.Flatten(pair => pair.Value.DependentTypes))
            .Select(kvp => new DependentType(kvp.Key, kvp.Value));
    }
}