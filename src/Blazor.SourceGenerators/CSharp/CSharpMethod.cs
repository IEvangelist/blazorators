// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

internal record CSharpMethod(
    string RawName,
    string RawReturnTypeName,
    List<CSharpType> ParameterDefinitions,
    JavaScriptMethod? JavaScriptMethodDependency = null) : ICSharpDependencyGraphObject
{
    public bool IsPureJavaScriptInvocation =>
        JavaScriptMethodDependency is { IsPure: true };

    public bool IsNotBiDirectionalJavaScript =>
        JavaScriptMethodDependency is { IsBiDirectionalJavaScript: false };

    public bool IsReturnTypeNullable =>
        RawReturnTypeName.Contains("null");

    public bool IsVoid => RawReturnTypeName is "void";

    public Dictionary<string, CSharpObject> DependentTypes { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> dependentTypes = new(StringComparer.OrdinalIgnoreCase);
            if (ParameterDefinitions is { Count: > 0 })
            {
                foreach (var kvp
                    in ParameterDefinitions.SelectMany(pd => pd.DependentTypes)
                        .Flatten(pair => pair.Value.DependentTypes))
                {
                    dependentTypes[kvp.Key] = kvp.Value;
                }
            }
            if (JavaScriptMethodDependency is { ParameterDefinitions.Count: > 0 })
            {
                foreach (var dependency
                    in JavaScriptMethodDependency.ParameterDefinitions.SelectMany(pd => pd.DependentTypes)
                        .Flatten(pair => pair.Value.DependentTypes))
                {
                    dependentTypes[dependency.Key] = dependency.Value;
                }
            }

            return dependentTypes.Select(kvp => (kvp.Key, kvp.Value))
                .Concat(this.GetAllDependencies())
                .ToImmutableHashSet();
        }
    }
}
