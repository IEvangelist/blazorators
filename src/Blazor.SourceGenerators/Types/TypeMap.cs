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

                // Generic-fallback / non-primitive TS object types: the consumer
                // is expected to model these as a CLR `object` (which round-trips
                // through `System.Text.Json` as a `JsonElement`). Mapping them
                // here prevents the parser from recursing into `_reader.TryGetDeclaration`
                // and either looping forever or emitting an empty DTO class.
                ["any"] = "object",
                ["unknown"] = "object",
                ["object"] = "object",
                ["any | null"] = "object?",
                ["unknown | null"] = "object?",
                ["object | null"] = "object?",

                // TS `void` is only legal in return position. We still map it so
                // primitive-detection short-circuits for methods that return it.
                ["void"] = "void",

                // BigInt round-trips poorly through System.Text.Json; the closest
                // CLR primitive without forcing a `System.Numerics` dependency on
                // consumers is `long`. Lossy for values outside [Int64.MinValue,
                // Int64.MaxValue]; documented as such on the BlazorHostingModel
                // attribute.
                ["bigint"] = "long",
                ["bigint | null"] = "long?",

                // Typed-array views map to native CLR arrays so consumers can use
                // them directly without an extra JS-interop hop. Note that JS
                // serialization may copy the buffer.
                ["ArrayBuffer"] = "byte[]",
                ["ArrayBuffer | null"] = "byte[]?",
                ["Uint8Array"] = "byte[]",
                ["Uint8Array | null"] = "byte[]?",
                ["Uint8ClampedArray"] = "byte[]",
                ["Uint8ClampedArray | null"] = "byte[]?",
                ["Uint16Array"] = "ushort[]",
                ["Uint16Array | null"] = "ushort[]?",
                ["Uint32Array"] = "uint[]",
                ["Uint32Array | null"] = "uint[]?",
                ["Int8Array"] = "sbyte[]",
                ["Int8Array | null"] = "sbyte[]?",
                ["Int16Array"] = "short[]",
                ["Int16Array | null"] = "short[]?",
                ["Int32Array"] = "int[]",
                ["Int32Array | null"] = "int[]?",
                ["BigInt64Array"] = "long[]",
                ["BigInt64Array | null"] = "long[]?",
                ["BigUint64Array"] = "ulong[]",
                ["BigUint64Array | null"] = "ulong[]?",
                ["Float32Array"] = "float[]",
                ["Float32Array | null"] = "float[]?",
                ["Float64Array"] = "double[]",
                ["Float64Array | null"] = "double[]?",
            };

        // Precomputed value set so `IsPrimitiveType` avoids an O(n) scan of
        // `_primitiveTypeMap.Values` on every check. Primitive detection runs
        // for every parsed property/method-parameter/return-type, so the cost
        // adds up across the ~800KB of lib.dom.d.ts.
        internal static readonly HashSet<string> _csharpPrimitiveValues =
            new(_primitiveTypeMap.Values, StringComparer.OrdinalIgnoreCase);

        internal bool IsPrimitiveType(string typeScriptType) =>
            _primitiveTypeMap.ContainsKey(typeScriptType) ||
            _csharpPrimitiveValues.Contains(typeScriptType);

        internal string this[string typeScriptType] =>
            _primitiveTypeMap.TryGetValue(typeScriptType, out var csharpType) ? csharpType : typeScriptType;
    }
}
