// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace TypeScript.TypeConverter;

internal record CSharpObject(
    string TypeName,
    string? ExtendsTypeName)
{
    public Dictionary<string, (bool IsNullable, string TypeName)> Members { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public sealed override string ToString()
    {
        StringBuilder builder = new("namespace Microsoft.JSInterop;");
        builder.Append("\r\n\r\n");

        var memberCount = Members.Count;
        if (ExtendsTypeName is null)
        {
            builder.Append($"public record {TypeName}(\r\n");

            foreach (var (index, (memberName, (isNullable, memberType))) in Members.Select((kvp, index) => (index, kvp)))
            {
                var statementTerminator = index + 1 < memberCount ? "," : "";
                var nullableExpression = isNullable ? "?" : "";
                builder.Append(
                    $"    {memberType}{nullableExpression} {memberName.CapitalizeFirstLetter()}{statementTerminator}\r\n");
            }

            builder.Append(");\r\n");
        }
        else
        {
            builder.Append($"public class {TypeName} : {ExtendsTypeName}\r\n");
            builder.Append("{\r\n");

            foreach (var (index, (memberName, (isNullable, memberType))) in Members.Select((kvp, index) => (index, kvp)))
            {
                var nullableExpression = isNullable ? "?" : "";

                builder.Append(
                    $"    public {memberType}{nullableExpression} {memberName.CapitalizeFirstLetter()} {{ get; set; }}\r\n");
            }

            builder.Append("}\r\n");
        }

        return builder.ToString();
    }
}
