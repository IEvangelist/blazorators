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
        builder.AppendLine();
        builder.AppendLine();

        var memberCount = Members.Count;
        if (ExtendsTypeName is null)
        {
            builder.AppendLine($"public record {TypeName}(");

            foreach (var (index, (memberName, (isNullable, memberType))) in Members.Select((kvp, index) => (index, kvp)))
            {
                var statementTerminator = index + 1 < memberCount ? "," : "";
                var nullableExpression = isNullable ? "?" : "";
                builder.AppendLine(
                    $"    {memberType}{nullableExpression} {memberName.CapitalizeFirstLetter()}{statementTerminator}");
            }

            builder.AppendLine(");");
        }
        else
        {
            builder.AppendLine($"public class {TypeName} : {ExtendsTypeName}");
            builder.AppendLine("{");

            foreach (var (index, (memberName, (isNullable, memberType))) in Members.Select((kvp, index) => (index, kvp)))
            {
                var nullableExpression = isNullable ? "?" : "";

                builder.AppendLine(
                    $"    public {memberType}{nullableExpression} {memberName.CapitalizeFirstLetter()} {{ get; set; }}");
            }

            builder.AppendLine("}");
        }

        return builder.ToString();
    }
}
