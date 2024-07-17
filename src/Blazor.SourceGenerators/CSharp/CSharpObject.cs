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
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IImmutableSet<DependentType> AllDependentTypes => this.GetAllDependencies()
                .Concat(Properties.SelectMany(p => p.Value.AllDependentTypes))
                .Concat(Methods.SelectMany(p => p.Value.AllDependentTypes))
                .Concat([new DependentType(TypeName, this)])
                .ToImmutableHashSet(DependentTypeComparer.Default);

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpProperty"/> details.
    /// </summary>
    public Dictionary<string, CSharpProperty> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpMethod"/> details.
    /// </summary>
    public Dictionary<string, CSharpMethod> Methods { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsActionParameter =>
        TypeName.EndsWith("Callback");

    public override string ToString()
    {
        StringBuilder builder = new("#nullable enable\r\n");

        builder.Append("using System.Text.Json.Serialization;\r\n\r\n");
        builder.Append("namespace Microsoft.JSInterop;\r\n\r\n");

        builder.Append($"/// <summary>\r\n");
        builder.Append($"/// Source-generated object representing an ideally immutable <c>{TypeName}</c> value.\r\n");
        builder.Append($"/// </summary>\r\n");
        builder.Append($"public class {TypeName}\r\n{{\r\n");

        foreach (var (index, kvp)
            in Properties.Select((kvp, index) => (index, kvp)))
        {
            var (memberName, member) = (kvp.Key, kvp.Value);
            var typeName = member.MappedTypeName;
            var nullableExpression = member.IsNullable && !typeName.EndsWith("?") ? "?" : "";
            var trivia = member.IsArray ? "[]" : "";
            var isPrimitive = Primitives.IsPrimitiveType(typeName);
            var statementTerminator = member.IsNullable ||
                typeName is "string" || !isPrimitive ? " = default!;" : "";
            var csharpMemberName = memberName.CapitalizeFirstLetter();

            builder.Append($"    /// <summary>\r\n");
            builder.Append($"    /// Source-generated property representing the <c>{TypeName}.{memberName}</c> value.\r\n");
            builder.Append($"    /// </summary>\r\n");
            builder.Append($"    [JsonPropertyName(\"{memberName}\")]\r\n");
            builder.Append($"    public {typeName}{trivia}{nullableExpression} {csharpMemberName} {{ get; set; }}{statementTerminator}\r\n");

            // Add readonly property for converting DOMTimeStamp (long) to DateTime.
            if (member.RawTypeName is "DOMTimeStamp" or "DOMTimeStamp | null"
                or "EpochTimeStamp" or "EpochTimeStamp | null")
            {
                builder.Append($"    /// <summary>\r\n");
                builder.Append($"    /// Source-generated property representing the <c>{TypeName}.{memberName}</c> value, \r\n");
                builder.Append($"    /// converted as a <see cref=\"System.DateTime\" /> in UTC.\r\n");
                builder.Append($"    /// </summary>\r\n");

                var nullable = member.IsNullable ? "?" : "";
                builder.Append($"    [JsonIgnore]\r\n");
                builder.Append($"    public DateTime{nullable} {csharpMemberName}AsUtcDateTime => {csharpMemberName}.ToDateTimeFromUnix();\r\n");
            }
        }

        builder.Append("}\r\n");
        var result = builder.ToString();
        return result;
    }
}