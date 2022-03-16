// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpDependencyGraphExtensions
{
    internal static IImmutableSet<(string TypeName, CSharpObject Object)> GetAllDependencies(
        this ICSharpDependencyGraphObject dependencyGraphObject)
    {
        Dictionary<string, CSharpObject> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp
            in dependencyGraphObject.DependentTypes?.Flatten(
                obj => obj.Value.DependentTypes)
            ?? Enumerable.Empty<KeyValuePair<string, CSharpObject>>())
        {
            map[kvp.Key] = kvp.Value;
        }

        return map.Select(kvp => (kvp.Key, kvp.Value))
            .ToImmutableHashSet();
    }
}
