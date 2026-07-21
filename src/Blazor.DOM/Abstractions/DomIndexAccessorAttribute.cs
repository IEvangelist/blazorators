// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Identifies an explicit generated JavaScript indexed access operation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DomIndexAccessorAttribute : Attribute
{
    public DomIndexAccessorAttribute(
        DomAccessorOperation operation,
        DomIndexKeyKind keyKind,
        string keySourceType,
        DomTransportKind valueTransportKind,
        string valueSourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keySourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueSourceType);
        Operation = operation;
        KeyKind = keyKind;
        KeySourceType = keySourceType;
        ValueTransportKind = valueTransportKind;
        ValueSourceType = valueSourceType;
    }

    public DomAccessorOperation Operation { get; }
    public DomIndexKeyKind KeyKind { get; }
    public string KeySourceType { get; }
    public DomTransportKind ValueTransportKind { get; }
    public string ValueSourceType { get; }
    public bool Nullable { get; set; }
    public bool Streamable { get; set; }
    public bool StructuredClone { get; set; }
}

/// <summary>The JavaScript property-key domain used by an index signature.</summary>
public enum DomIndexKeyKind
{
    Number,
    String,
    Symbol,
}
