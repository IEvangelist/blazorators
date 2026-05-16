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

            // `Record<K, V>` -> `Dictionary<TKey, TValue>` with both
            // type arguments routed through the primitive map. The
            // raw `Record<...>` token would otherwise be emitted into
            // the C# DTO type which is not a valid CLR type. DOM hits
            // include `RTCStats.parameterData: Record<string, number>`
            // and `PushSubscriptionJSON.keys: Record<string, string>`.
            if (TypeShape.TryGetRecordTypeArguments(RawTypeName, out var keyType, out var valueType))
            {
                var mappedKey = TypeMap.PrimitiveTypes[keyType];
                var mappedValue = TypeMap.PrimitiveTypes[valueType];
                return $"Dictionary<{mappedKey}, {mappedValue}>";
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

    public bool IsIndexer => RawName.StartsWith("[", StringComparison.Ordinal) && RawName.EndsWith("]", StringComparison.Ordinal);

    public bool IsArray =>
        TypeShape.IsArrayShape(TypeShape.StripNullClause(RawTypeName));
}
