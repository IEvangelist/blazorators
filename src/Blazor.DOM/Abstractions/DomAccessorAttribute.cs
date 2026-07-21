// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Identifies a generated member as a JavaScript property operation.</summary>
/// <remarks>
/// Asymmetric TypeScript accessors are emitted as a read-only property and a
/// separate setter method. Consumers must use this metadata rather than infer
/// JavaScript invocation semantics from the generated C# member name.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class DomAccessorAttribute : Attribute
{
    /// <summary>Creates metadata for one direction of a JavaScript property.</summary>
    /// <param name="propertyName">The exact JavaScript property name.</param>
    /// <param name="operation">Whether the annotated member reads or writes the property.</param>
    /// <param name="transportKind">The transport required for this direction.</param>
    /// <param name="sourceType">The exact TypeScript source type.</param>
    public DomAccessorAttribute(
        string propertyName,
        DomAccessorOperation operation,
        DomTransportKind transportKind,
        string sourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);

        PropertyName = propertyName;
        Operation = operation;
        TransportKind = transportKind;
        SourceType = sourceType;
    }

    /// <summary>The exact JavaScript property name.</summary>
    public string PropertyName { get; }

    /// <summary>The JavaScript property operation represented by the member.</summary>
    public DomAccessorOperation Operation { get; }

    /// <summary>The transport required for this operation.</summary>
    public DomTransportKind TransportKind { get; }

    /// <summary>The exact TypeScript source type.</summary>
    public string SourceType { get; }

    /// <summary>Whether the directional TypeScript type accepts null or undefined.</summary>
    public bool Nullable { get; set; }

    /// <summary>Whether the directional value can expose bytes through a JS stream.</summary>
    public bool Streamable { get; set; }

    /// <summary>Whether structured cloning is supported for the directional value.</summary>
    public bool StructuredClone { get; set; }

    /// <summary>The reviewed reason for an unsupported transport.</summary>
    public string? UnsupportedReason { get; set; }
}

/// <summary>A generated JavaScript property operation.</summary>
public enum DomAccessorOperation
{
    /// <summary>Reads a JavaScript property.</summary>
    Get,

    /// <summary>Writes a JavaScript property.</summary>
    Set,
}
