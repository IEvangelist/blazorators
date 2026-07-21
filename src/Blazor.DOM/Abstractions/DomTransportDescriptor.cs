// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Generated-code metadata describing how a TypeScript value crosses the JS/.NET boundary.
/// </summary>
public sealed record DomTransportDescriptor
{
    private DomTransportDescriptor(
        DomTransportKind kind,
        string sourceType,
        bool nullable,
        bool streamable,
        bool structuredClone,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException("A TypeScript source type is required.", nameof(sourceType));
        }
        if (kind == DomTransportKind.Unsupported && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Unsupported transports require a precise reason.",
                nameof(reason));
        }
        if (kind != DomTransportKind.Unsupported && reason is not null)
        {
            throw new ArgumentException(
                "Supported transports cannot have an unsupported reason.",
                nameof(reason));
        }

        Kind = kind;
        SourceType = sourceType;
        Nullable = nullable;
        Streamable = streamable;
        StructuredClone = structuredClone;
        Reason = reason;
    }

    /// <summary>The selected transport.</summary>
    public DomTransportKind Kind { get; }

    /// <summary>The original TypeScript type text.</summary>
    public string SourceType { get; }

    /// <summary>Whether JavaScript may supply <see langword="null"/>.</summary>
    public bool Nullable { get; }

    /// <summary>Whether this value can also expose byte content through a JS stream reference.</summary>
    public bool Streamable { get; }

    /// <summary>
    /// Whether Web IDL permits structured cloning. This flag never grants JSON compatibility.
    /// </summary>
    public bool StructuredClone { get; }

    /// <summary>Why the value is unsupported, or <see langword="null"/> for supported transports.</summary>
    public string? Reason { get; }

    /// <summary>Creates metadata for a reviewed JSON value.</summary>
    public static DomTransportDescriptor JsonValue(string sourceType, bool nullable = false) =>
        new(DomTransportKind.JsonValue, sourceType, nullable, false, true, null);

    /// <summary>Creates metadata for a live JS object reference.</summary>
    public static DomTransportDescriptor JsReference(
        string sourceType,
        bool nullable = false,
        bool streamable = false,
        bool structuredClone = false) =>
        new(
            DomTransportKind.JsReference,
            sourceType,
            nullable,
            streamable,
            structuredClone,
            null);

    /// <summary>Creates metadata for a JS stream reference.</summary>
    public static DomTransportDescriptor JsStream(string sourceType, bool nullable = false) =>
        new(DomTransportKind.JsStream, sourceType, nullable, true, false, null);

    /// <summary>Creates metadata for an ArrayBuffer, typed array, or other binary shape.</summary>
    public static DomTransportDescriptor Binary(
        string sourceType,
        bool nullable = false,
        bool streamable = true) =>
        new(DomTransportKind.Binary, sourceType, nullable, streamable, true, null);

    /// <summary>Creates metadata for a Web IDL transferable value.</summary>
    public static DomTransportDescriptor Transferable(
        string sourceType,
        bool nullable = false) =>
        new(DomTransportKind.Transferable, sourceType, nullable, false, true, null);

    /// <summary>Defers transport selection to the closed CLR result type.</summary>
    public static DomTransportDescriptor Inferred(
        string sourceType,
        bool nullable = false) =>
        new(DomTransportKind.Inferred, sourceType, nullable, false, true, null);

    /// <summary>Creates metadata for an API-defined structured-clone value boundary.</summary>
    public static DomTransportDescriptor StructuredCloneValue(
        string sourceType,
        bool nullable = false) =>
        new(DomTransportKind.StructuredClone, sourceType, nullable, false, true, null);

    /// <summary>Creates metadata for an ambiguous or unsupported TypeScript shape.</summary>
    public static DomTransportDescriptor Unsupported(
        string sourceType,
        string reason,
        bool nullable = false) =>
        new(DomTransportKind.Unsupported, sourceType, nullable, false, false, reason);

    internal void RequireReference(string parameterName)
    {
        if (Kind == DomTransportKind.Unsupported)
        {
            throw new DomTransportException(
                $"TypeScript value '{SourceType}' is unsupported: {Reason}");
        }
        if (Kind is not (DomTransportKind.JsReference or DomTransportKind.Transferable))
        {
            throw new ArgumentException(
                $"Transport '{Kind}' for TypeScript value '{SourceType}' is not a JS reference.",
                parameterName);
        }
    }

    internal void RequireStreamable(string parameterName)
    {
        if (Kind == DomTransportKind.Unsupported)
        {
            throw new DomTransportException(
                $"TypeScript value '{SourceType}' is unsupported: {Reason}");
        }
        if (!Streamable)
        {
            throw new ArgumentException(
                $"Transport '{Kind}' for TypeScript value '{SourceType}' is not streamable.",
                parameterName);
        }
    }
}
