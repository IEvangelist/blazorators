// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter;

/// <summary>
/// A record the represents various C# members, such as properties, methods, and events.
/// </summary>
internal record CSharpMember(
    string Name,
    string TypeName,
    bool IsNullable = false,
    bool IsReadonly = false)
{
    public string MappedTypeName => TypeMap.PrimitiveTypes[TypeName];
}