// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace TypeScript.TypeConverter.Types
{
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
                    ["Date"] = "DateTime"
                    //["Array"] = "[]"
                };

            internal bool IsPrimitiveType(string typeScriptType) =>
                _primitiveTypeMap.ContainsKey(typeScriptType);

            internal string this[string typeScriptType] =>
                _primitiveTypeMap.TryGetValue(typeScriptType, out var csharpType) ? csharpType : typeScriptType;
        }
    }
}