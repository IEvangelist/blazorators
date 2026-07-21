// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Records the exact JavaScript member name when CLR collision handling renames it.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
public sealed class DomJavaScriptNameAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A JavaScript member name is required.", nameof(name))
        : name;
}
