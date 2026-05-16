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
        var bareType = property.IsArray
            ? $"{property.MappedTypeName}[]"
            : property.MappedTypeName;

        return (
            ReturnType: options.IsWebAssembly ? bareType : $"ValueTask<{bareType}>",
            BareType: bareType);
    }
}
