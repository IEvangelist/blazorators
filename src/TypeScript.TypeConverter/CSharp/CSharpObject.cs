// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace TypeScript.TypeConverter.CSharp;

/// <summary>
/// A record the represents various C# objects.
/// </summary>
internal record CSharpObject(
    string TypeName,
    string? ExtendsTypeName)
{
    /// <summary>
    /// Gets or sets if the object is considered a method parameter. This
    /// changes the <see cref="ToString"/> behavior.
    /// </summary>
    public bool IsParameter { get; init; }

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpProperty"/> details.
    /// </summary>
    public Dictionary<string, CSharpProperty> Members { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    internal string ToParameterString()
    {
        return "";
    }

    internal string ToClassString()
    {
        StringBuilder builder = new("namespace Microsoft.JSInterop;");

        builder.Append("\r\n\r\n");

        var memberCount = Members.Count;
        builder.Append($"public class {TypeName} : {ExtendsTypeName}\r\n");
        builder.Append("{\r\n");

        foreach (var (index, (memberName, member)) in Members.Select((kvp, index) => (index, kvp)))
        {
            var nullableExpression = member.IsNullable ? "?" : "";

            builder.Append(
                $"    public {member.MappedTypeName}{nullableExpression} {memberName.CapitalizeFirstLetter()} {{ get; set; }}\r\n");
        }

        builder.Append("}\r\n");
        return builder.ToString();
    }

    internal string ToRecordString()
    {
        StringBuilder builder = new("namespace Microsoft.JSInterop;");

        builder.Append("\r\n\r\n");
        builder.Append($"public record {TypeName}(\r\n");

        var memberCount = Members.Count;
        foreach (var (index, (memberName, member)) in Members.Select((kvp, index) => (index, kvp)))
        {
            var statementTerminator = index + 1 < memberCount ? "," : "";
            var nullableExpression = member.IsNullable ? "?" : "";
            builder.Append(
                $"    {member.MappedTypeName}{nullableExpression} {memberName.CapitalizeFirstLetter()}{statementTerminator}\r\n");
        }

        builder.Append(");\r\n");
        return builder.ToString();
    }

    internal string ToStaticClassString()
    {
        return "";
    }

    public sealed override string ToString()
    {
        if (IsParameter && Members is { Count: 1 })
        {
            // TODO: Return simplified parameter declaration text.
            return $"";
        }

        return ExtendsTypeName is null ? ToRecordString() : ToClassString();
    }
}
