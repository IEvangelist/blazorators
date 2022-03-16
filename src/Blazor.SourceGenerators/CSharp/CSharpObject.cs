// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// A record the represents various C# objects.
/// </summary>
internal record CSharpObject(
    string TypeName,
    string? ExtendsTypeName) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (var prop
                in this.GetAllDependencies()
                    .Concat(Properties.SelectMany(
                        p => p.Value.AllDependentTypes))
                    .Concat(Methods.SelectMany(
                        p => p.Value.AllDependentTypes)))
            {
                result[prop.TypeName] = prop.Object;
            }

            return result.Select(kvp => (kvp.Key, kvp.Value))
                .Concat(new[] { (TypeName, Object: this) })
                .ToImmutableHashSet();
        }
    }

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpProperty"/> details.
    /// </summary>
    public Dictionary<string, CSharpProperty> Properties { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpMethod"/> details.
    /// </summary>
    public Dictionary<string, CSharpMethod> Methods { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsActionParameter =>
        TypeName.EndsWith("Callback");

    internal string ToClassString()
    {
        StringBuilder builder = new("#nullable enable\r\n");

        builder.Append("namespace Microsoft.JSInterop;\r\n\r\n");
        builder.Append($"public class {TypeName}\r\n{{\r\n");

        var memberCount = Properties.Count;
        foreach (var (index, kvp) in Properties.Select((kvp, index) => (index, kvp)))
        {
            var (memberName, member) = (kvp.Key, kvp.Value);
            var typeName = member.MappedTypeName;
            var nullableExpression = member.IsNullable && !typeName.EndsWith("?") ? "?" : "";
            var trivia = member.IsArray ? "[]" : "";
            var statementTerminator = member.IsNullable ? " = default!;" : "";

            builder.Append(
                $"    public {typeName}{trivia}{nullableExpression} {memberName.CapitalizeFirstLetter()} {{ get; set; }}{statementTerminator}\r\n");
        }

        builder.Append("}\r\n");
        var result = builder.ToString();
        return result;
    }

    public override string ToString() => ToClassString();
}
