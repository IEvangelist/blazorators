// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Transport used for a generated TypeScript value at the JS/.NET boundary.</summary>
public enum DomTransportKind
{
    /// <summary>A reviewed JSON value: primitive, enum, dictionary, or annotated value shape.</summary>
    JsonValue,

    /// <summary>A live JavaScript object represented by an <see cref="IJSObjectReference"/>.</summary>
    JsReference,

    /// <summary>Byte data supplied through an <see cref="IJSStreamReference"/>.</summary>
    JsStream,

    /// <summary>An ArrayBuffer, ArrayBufferView, Blob byte view, or equivalent binary shape.</summary>
    Binary,

    /// <summary>A Web IDL transferable value. Transferability does not imply JSON compatibility.</summary>
    Transferable,

    /// <summary>The CLR result type selects live-reference or structured-clone transport.</summary>
    Inferred,

    /// <summary>An arbitrary value crossing an API-defined structured-clone boundary.</summary>
    StructuredClone,

    /// <summary>A shape whose transport cannot be selected safely without an explicit escape hatch.</summary>
    Unsupported,
}
