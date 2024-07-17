// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// Represents a C# method which might have dependencies on JavaScript methods.
/// </summary>
internal record CSharpMethod(
    string RawName,
    string RawReturnTypeName,
    IList<CSharpType> ParameterDefinitions,
    JavaScriptMethod? JavaScriptMethodDependency = null) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// Indicates whether the method is a pure JavaScript invocation.
    /// </summary>
    public bool IsPureJavaScriptInvocation => JavaScriptMethodDependency?.IsPure == true;

    /// <summary>
    /// Indicates whether the method is not bi-directional JavaScript.
    /// </summary>
    public bool IsNotBiDirectionalJavaScript => JavaScriptMethodDependency?.IsBiDirectionalJavaScript == false;

    /// <summary>
    /// Indicates whether the return type of the method is nullable.
    /// </summary>
    public bool IsReturnTypeNullable => RawReturnTypeName.Contains("null");

    /// <summary>
    /// Indicates whether the method returns void.
    /// </summary>
    public bool IsVoid => RawReturnTypeName == "void";

    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all dependent types of this C# method, including those from parameters and JavaScript dependencies.
    /// </summary>
    public IImmutableSet<DependentType> AllDependentTypes => ParameterDefinitions.GetDependentTypes()
        .Concat(JavaScriptMethodDependency?.ParameterDefinitions.GetDependentTypes())
        .ToImmutableHashSet(DependentTypeComparer.Default);
}