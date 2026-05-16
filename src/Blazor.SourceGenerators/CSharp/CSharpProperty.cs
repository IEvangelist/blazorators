// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// A record the represents various C# members, such as properties, delegates and events.
/// </summary>
internal record CSharpProperty(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    bool IsReadonly = false) : CSharpType(RawName, RawTypeName, IsNullable)
{
    public string MappedTypeName
    {
        get
        {
            var direct = TypeMap.PrimitiveTypes[RawTypeName];
            if (!string.Equals(direct, RawTypeName, StringComparison.Ordinal))
            {
                return direct;
            }

            if (TypeShape.TryGetArrayElementTypeName(RawTypeName, out var elementTypeName))
            {
                return TypeMap.PrimitiveTypes[elementTypeName];
            }

            if (IsNullable)
            {
                // Strip both forms of the nullable clause -- the DOM
                // uses `T | undefined` for several Promise-returned
                // properties and `T | null` for everything else. Both
                // map onto a C# nullable type; the wrapping emitter
                // appends the `?` suffix based on `IsNullable`.
                return TypeShape.StripNullClause(RawTypeName);
            }

            return RawTypeName;
        }
    }

    public bool IsIndexer => RawName.StartsWith("[") && RawName.EndsWith("]");

    public bool IsArray =>
        TypeShape.IsArrayShape(TypeShape.StripNullClause(RawTypeName));
}
