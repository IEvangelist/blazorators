// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Identifies a generated member whose JavaScript key is a well-known symbol.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
public sealed class DomSymbolAttribute(DomWellKnownSymbol symbol) : Attribute
{
    public DomWellKnownSymbol Symbol { get; } = symbol;
}

/// <summary>Well-known JavaScript symbols used by generated DOM contracts.</summary>
public enum DomWellKnownSymbol
{
    Iterator,
    AsyncIterator,
}
