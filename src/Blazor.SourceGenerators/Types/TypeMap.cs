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
                ["URL | null"] = "Uri?",
                ["URL | undefined"] = "Uri?",
                ["URL"] = "Uri",
                ["URLSearchParams"] = "Uri",
                ["URLSearchParams | null"] = "Uri?",
                ["URLSearchParams | undefined"] = "Uri?",
                ["ArrayBuffer"] = "byte[]",
                ["ArrayBuffer | null"] = "byte[]?",
                ["ArrayBuffer | undefined"] = "byte[]?",
                ["ArrayBufferView"] = "byte[]",
                ["ArrayBufferView | null"] = "byte[]?",
                ["ArrayBufferView | undefined"] = "byte[]?",
                ["Blob"] = "byte[]",
                ["Blob | null"] = "byte[]?",
                ["Blob | undefined"] = "byte[]?",
                ["DataView"] = "byte[]",
                ["DataView | null"] = "byte[]?",
                ["DataView | undefined"] = "byte[]?",
                ["FormData"] = "byte[]",
                ["FormData | null"] = "byte[]?",
                ["FormData | undefined"] = "byte[]?",
                ["ReadableStream<Uint8Array>"] = "byte[]",
                ["ReadableStream<Uint8Array> | null"] = "byte[]?",
                ["ReadableStream<Uint8Array> | undefined"] = "byte[]?",
                ["Uint8Array"] = "byte[]",
                ["Uint8Array | null"] = "byte[]?",
                ["Uint8Array | undefined"] = "byte[]?",
                ["Uint8ClampedArray"] = "byte[]",
                ["Uint8ClampedArray | null"] = "byte[]?",
                ["Uint8ClampedArray | undefined"] = "byte[]?",
                //["Array"] = "[]"  
            };

        internal bool IsPrimitiveType(string typeScriptType) =>
            _primitiveTypeMap.ContainsKey(typeScriptType) ||
            _primitiveTypeMap.Values.Any(value => value == typeScriptType);

        internal string this[string typeScriptType] =>
            _primitiveTypeMap.TryGetValue(typeScriptType, out var csharpType) ? csharpType : typeScriptType;
    }
}
