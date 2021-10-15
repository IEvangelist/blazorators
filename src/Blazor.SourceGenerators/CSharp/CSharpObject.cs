// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TypeScript.TypeConverter.Extensions;

namespace TypeScript.TypeConverter.CSharp
{
    /// <summary>
    /// A record the represents various C# objects.
    /// </summary>
    public record CSharpObject(
        string TypeName,
        string? ExtendsTypeName)
    {
        /// <summary>
        /// The collection of types that this object depends on.
        /// </summary>
        public Dictionary<string, CSharpObject> DependentTypes { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

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
            if (IsActionParameter) return "";

            StringBuilder builder = new("namespace Microsoft.JSInterop");

            builder.Append("{\r\n\r\n");

            var memberCount = Properties.Count;
            builder.Append($"    public class {TypeName} : {ExtendsTypeName}\r\n");
            builder.Append("    {\r\n");

            foreach (var (index, kvp) in Properties.Select((kvp, index) => (index, kvp)))
            {
                var (memberName, member) = (kvp.Key, kvp.Value);
                var nullableExpression = member.IsNullable ? "?" : "";

                builder.Append(
                    $"        public {member.MappedTypeName}{nullableExpression} {memberName.CapitalizeFirstLetter()} {{ get; set; }}\r\n");
            }

            builder.Append("    }\r\n");
            builder.Append("}\r\n");
            return builder.ToString();
        }

        internal string ToRecordString()
        {
            StringBuilder builder = new("namespace Microsoft.JSInterop;");

            builder.Append("\r\n\r\n");
            builder.Append($"public record {TypeName}(\r\n");

            var memberCount = Properties.Count;
            foreach (var (index, kvp) in Properties.Select((kvp, index) => (index, kvp)))
            {
                var (memberName, member) = (kvp.Key, kvp.Value);
                var statementTerminator = index + 1 < memberCount ? "," : "";
                var nullableExpression = member.IsNullable ? "?" : "";
                builder.Append(
                    $"    {member.MappedTypeName}{nullableExpression} {memberName.CapitalizeFirstLetter()}{statementTerminator}\r\n");
            }

            builder.Append(");\r\n");
            return builder.ToString();
        }

        public override string ToString()
        {
            return ExtendsTypeName is null ? ToRecordString() : ToClassString();
        }
    }
}