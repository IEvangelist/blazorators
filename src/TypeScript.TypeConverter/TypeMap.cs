// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter;

static class TypeMap
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
                //["Array"] = "[]"
            };

        internal string this[string typeScriptType] =>
            _primitiveTypeMap.TryGetValue(typeScriptType, out var csharpType) ? csharpType : typeScriptType;
    }
}
