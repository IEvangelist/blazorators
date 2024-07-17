// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpDependencyGraphExtensions
{
    internal static IImmutableSet<(string TypeName, CSharpObject Object)> GetAllDependencies(this ICSharpDependencyGraphObject dependencyGraphObject)
    {
        Dictionary<string, CSharpObject> map = new(StringComparer.OrdinalIgnoreCase);
        var flattened = dependencyGraphObject.DependentTypes?.Flatten(obj => obj.Value.DependentTypes) ?? [];

        foreach (var kvp in flattened)
        {
            map[kvp.Key] = kvp.Value;
        }

        return map.Select(kvp => (kvp.Key, kvp.Value))
            .ToImmutableHashSet();
    }
}