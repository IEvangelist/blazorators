// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpPropertyExtensions
{
    internal static (string ReturnType, string BareType) GetPropertyTypes(
        this CSharpProperty property, GeneratorOptions options)
    {
        // `CSharpProperty.MappedTypeName` deliberately returns the element
        // type for array-shaped TypeScript types (`T[]`, `Array<T>`,
        // `ReadonlyArray<T>`) so that primitive-type lookups and
        // dependency-graph walks can ignore the array wrapper. The emitted
        // C# property must still carry the `[]` suffix - otherwise a TS
        // `readonly string[]` would surface in the interface as a plain
        // `string`. Mirrors the array handling in `CSharpObject.AppendProperty`.
        //
        // Also restore the nullable annotation for non-primitive nullable
        // properties. The direct primitive map carries explicit
        // `"T | null" -> "T?"` entries, so `MappedTypeName` for a primitive
        // nullable already ends with `?`. For custom DTO types and
        // arrays of either kind, `MappedTypeName` strips the trailing
        // `| null` / `| undefined` clause but does not append the C#
        // `?` suffix; without this re-attach, a TS
        // `readonly currentEntry: NavigationHistoryEntry | null`
        // emitted as `NavigationHistoryEntry CurrentEntry { get; }`
        // (CS8618 in the consumer) and `readonly entries: Entry[] | null`
        // dropped both the array nullability and any future warning.
        // Mirrors the parallel logic in `CSharpObject.AppendProperty`.
        var mappedTypeName = property.MappedTypeName;
        var arraySuffix = property.IsArray ? "[]" : "";
        var nullableSuffix = property.IsNullable && !mappedTypeName.EndsWith("?", StringComparison.Ordinal) ? "?" : "";
        var bareType = $"{mappedTypeName}{arraySuffix}{nullableSuffix}";

        return (
            ReturnType: options.IsWebAssembly ? bareType : $"ValueTask<{bareType}>",
            BareType: bareType);
    }
}
