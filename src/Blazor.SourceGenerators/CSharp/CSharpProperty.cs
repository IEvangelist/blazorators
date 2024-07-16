// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// A record that represents various C# members, such as properties, delegates, and events.
/// </summary>
internal record CSharpProperty(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    bool IsReadonly = false) : CSharpType(RawName, RawTypeName, IsNullable)
{
    /// <summary>
    /// Gets the mapped type name, resolving primitive types and handling arrays and nullability.
    /// </summary>
    public string MappedTypeName => GetMappedTypeName();

    /// <summary>
    /// Determines if the property is an indexer.
    /// </summary>
    public bool IsIndexer => RawName.StartsWith("[") && RawName.EndsWith("]");

    /// <summary>
    /// Determines if the property is an array.
    /// </summary>
    public bool IsArray => RawTypeName.EndsWith("[]") || (RawTypeName.StartsWith("ReadonlyArray<") && RawTypeName.EndsWith(">"));

    private string GetMappedTypeName()
    {
        var mappedTypeName = Primitives.Instance[RawTypeName];

        if (IsArray)
        {
            mappedTypeName = mappedTypeName
                .Replace("[]", "")
                .Replace("ReadonlyArray<", "")
                .Replace(">", "");
        }

        if (IsNullable)
        {
            mappedTypeName = mappedTypeName.Replace("| null", "");
        }

        return mappedTypeName;
    }
}