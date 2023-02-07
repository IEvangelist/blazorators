// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Types;

internal static class TypeMap
{
    internal static readonly Primitives PrimitiveTypes = new();

    internal class Primitives
    {
        internal static readonly Dictionary<string, string> _primitiveTypeMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // The JavaScript Number type is a double-precision 64-bit binary format IEEE 754 value
                ["number"] = "double",
                ["string"] = "string",
                ["boolean"] = "bool",
                ["enum"] = "enum",
                ["Date"] = "DateTime",
                ["DOMTimeStamp"] = "long",
                ["EpochTimeStamp"] = "long",
                ["number | null"] = "double?",
                ["string | null"] = "string?",
                ["boolean | null"] = "bool?",
                ["enum | null"] = "enum?",
                ["Date | null"] = "DateTime?",
                ["DOMTimeStamp | null"] = "long?",
                ["EpochTimeStamp | null"] = "long?",
                //["Array"] = "[]"
            };

        internal bool IsPrimitiveType(string typeScriptType) =>
            _primitiveTypeMap.ContainsKey(typeScriptType) ||
            _primitiveTypeMap.Values.Any(value => value == typeScriptType);

        internal string this[string typeScriptType] =>
            _primitiveTypeMap.TryGetValue(typeScriptType, out var csharpType) ? csharpType : typeScriptType;
    }
}
