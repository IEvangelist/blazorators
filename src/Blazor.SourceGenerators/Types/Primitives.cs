// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Types;

internal class Primitives
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
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
        ["number | undefined"] = "double?",
        ["string | null"] = "string?",
        ["string | undefined"] = "string?",
        ["boolean | null"] = "bool?",
        ["boolean | undefined"] = "bool?",
        ["enum | null"] = "enum?",
        ["enum | undefined"] = "enum?",
        ["Date | null"] = "DateTime?",
        ["Date | undefined"] = "DateTime?",
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

    internal static readonly Primitives Instance = new();

    internal string this[string typescript] => _map.TryGetValue(typescript, out var csharp)
        ? csharp
        : typescript;

    internal static bool IsPrimitiveType(string typeScriptType) =>
        _map.ContainsKey(typeScriptType) ||
        _map.Values.Any(value => value == typeScriptType);
}