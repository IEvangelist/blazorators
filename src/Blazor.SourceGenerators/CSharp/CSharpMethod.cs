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
        TypeShape.HasNullClause(RawReturnTypeName) ||
        RawReturnTypeName.Equals("null", StringComparison.Ordinal);

    public bool IsVoid =>
        RawReturnTypeName is "void" ||
        (IsPromise && PromiseUnwrappedTypeName is "void");

    /// <summary>
    /// True when the raw return type is a TypeScript
    /// <c>Promise&lt;T&gt;</c> shape. The DOM has hundreds of these
    /// (<c>Permissions.query()</c>, <c>Body.arrayBuffer()</c>,
    /// <c>AudioContext.close()</c>, ...). Without explicit handling
    /// the emitter dropped the verbatim <c>Promise&lt;T&gt;</c> token
    /// into the C# method signature, which is not a CLR type. The
    /// generator now peels the wrapper and forces the async invocation
    /// path (<c>InvokeAsync</c> / <c>ValueTask&lt;T&gt;</c>) even when
    /// hosting under WebAssembly, since a Promise cannot be resolved
    /// synchronously.
    /// </summary>
    public bool IsPromise =>
        RawReturnTypeName.StartsWith("Promise<", StringComparison.Ordinal) &&
        RawReturnTypeName.EndsWith(">", StringComparison.Ordinal);

    /// <summary>
    /// When <see cref="IsPromise"/> is <c>true</c>, returns the type
    /// argument of the <c>Promise&lt;...&gt;</c> shape (e.g.
    /// <c>"PermissionStatus"</c> for <c>Promise&lt;PermissionStatus&gt;</c>,
    /// <c>"void"</c> for <c>Promise&lt;void&gt;</c>). Otherwise
    /// returns <see cref="RawReturnTypeName"/> verbatim so callers can
    /// use this property as the single source of truth for the
    /// "bareTypeBeforeMapping" downstream of the parser.
    /// </summary>
    public string PromiseUnwrappedTypeName =>
        IsPromise
            ? RawReturnTypeName.Substring(
                "Promise<".Length,
                RawReturnTypeName.Length - "Promise<".Length - 1)
            : RawReturnTypeName;

    public Dictionary<string, CSharpObject> DependentTypes { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    private IImmutableSet<(string TypeName, CSharpObject Object)>? _allDependentTypes;

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            if (_allDependentTypes is not null)
            {
                return _allDependentTypes;
            }

            Dictionary<string, CSharpObject> dependentTypes = new(StringComparer.OrdinalIgnoreCase);
            if (ParameterDefinitions is { Count: > 0 })
            {
                foreach (var kvp
                    in ParameterDefinitions.SelectMany(pd => pd.DependentTypes)
                        .Flatten(
                            childSelector: pair => pair.Value.DependentTypes,
                            keySelector: pair => pair.Key,
                            keyComparer: StringComparer.OrdinalIgnoreCase))
                {
                    dependentTypes[kvp.Key] = kvp.Value;
                }
            }
            if (JavaScriptMethodDependency is { ParameterDefinitions.Count: > 0 })
            {
                foreach (var dependency
                    in JavaScriptMethodDependency.ParameterDefinitions.SelectMany(pd => pd.DependentTypes)
                        .Flatten(
                            childSelector: pair => pair.Value.DependentTypes,
                            keySelector: pair => pair.Key,
                            keyComparer: StringComparer.OrdinalIgnoreCase))
                {
                    dependentTypes[dependency.Key] = dependency.Value;
                }
            }

            return _allDependentTypes = dependentTypes.Select(kvp => (kvp.Key, kvp.Value))
                .Concat(this.GetAllDependencies())
                .ToImmutableHashSet();
        }
    }
}
