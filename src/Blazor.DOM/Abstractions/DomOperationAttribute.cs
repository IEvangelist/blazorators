// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Identifies an emitted interface method as one exact JavaScript operation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DomOperationAttribute : Attribute
{
    /// <summary>Creates authoritative dispatch metadata for a generated operation.</summary>
    public DomOperationAttribute(
        string logicalIdentity,
        string javaScriptName,
        DomTransportKind returnTransport,
        string returnSourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(javaScriptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnSourceType);

        LogicalIdentity = logicalIdentity;
        JavaScriptName = javaScriptName;
        ReturnTransport = returnTransport;
        ReturnSourceType = returnSourceType;
    }

    /// <summary>Stable host-neutral identity shared by Server and WebAssembly.</summary>
    public string LogicalIdentity { get; }

    /// <summary>The exact property, method, or well-known symbol source name.</summary>
    public string JavaScriptName { get; }

    /// <summary>The reviewed transport used by the operation result.</summary>
    public DomTransportKind ReturnTransport { get; }

    /// <summary>The exact TypeScript result type.</summary>
    public string ReturnSourceType { get; }

    /// <summary>Whether JavaScript can return null or undefined.</summary>
    public bool Nullable { get; set; }

    /// <summary>Whether the operation returns a Promise.</summary>
    public bool Promise { get; set; }

    /// <summary>Whether the result can be opened through a JS stream reference.</summary>
    public bool Streamable { get; set; }

    /// <summary>Whether the result supports structured cloning.</summary>
    public bool StructuredClone { get; set; }

    /// <summary>The reviewed reason for an unsupported result transport.</summary>
    public string? UnsupportedReason { get; set; }
}
