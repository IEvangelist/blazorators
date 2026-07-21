// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Maps a collision-safe generated member to its exact ambient JavaScript name.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
public sealed class DomGlobalAliasAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A JavaScript global name is required.", nameof(name))
        : name;
}
