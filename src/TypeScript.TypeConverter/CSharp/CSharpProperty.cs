// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TypeScript.TypeConverter.Types;

namespace TypeScript.TypeConverter.CSharp;

/// <summary>
/// A record the represents various C# members, such as properties, delegates and events.
/// </summary>
public record CSharpProperty(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    bool IsReadonly = false) : CSharpType(RawName, RawTypeName, IsNullable)
{
    public string MappedTypeName => TypeMap.PrimitiveTypes[RawTypeName];
}