// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Explicit escape hatch for TypeScript <c>any</c>, <c>unknown</c>, and <c>object</c>.
/// The declared transport is validated before the value reaches JS interop.
/// </summary>
public sealed class DomDynamicValue
{
    private DomDynamicValue(object? value, DomTransportDescriptor transport)
    {
        Value = value;
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>The value to transport.</summary>
    public object? Value { get; }

    /// <summary>The caller-selected transport.</summary>
    public DomTransportDescriptor Transport { get; }

    /// <summary>Creates a validated dynamic value with explicit transport metadata.</summary>
    public static DomDynamicValue Create(
        object? value,
        DomTransportDescriptor transport) =>
        new(value, transport);

    /// <summary>Creates a dynamic reviewed JSON value.</summary>
    public static DomDynamicValue Json<TValue>(TValue value, string sourceType = "unknown") =>
        new(value, DomTransportDescriptor.JsonValue(sourceType, value is null));

    /// <summary>Creates a dynamic live JS reference.</summary>
    public static DomDynamicValue Reference(
        IDomProxy? value,
        string sourceType = "unknown") =>
        new(value, DomTransportDescriptor.JsReference(sourceType, value is null));

    /// <summary>Creates a dynamic live JS reference.</summary>
    public static DomDynamicValue Reference(
        IJSObjectReference? value,
        string sourceType = "unknown") =>
        new(value, DomTransportDescriptor.JsReference(sourceType, value is null));

    /// <summary>Creates a dynamic binary value using Blazor's optimized byte-array transport.</summary>
    public static DomDynamicValue Binary(byte[] value, string sourceType = "unknown") =>
        new(
            value ?? throw new ArgumentNullException(nameof(value)),
            DomTransportDescriptor.Binary(sourceType));

    /// <summary>Creates a dynamic outgoing .NET stream reference.</summary>
    public static DomDynamicValue Stream(
        DotNetStreamReference value,
        string sourceType = "unknown") =>
        new(
            value ?? throw new ArgumentNullException(nameof(value)),
            DomTransportDescriptor.JsStream(sourceType));
}
