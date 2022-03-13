// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpPropertyExtensions
{
    internal static (string ReturnType, string BareType) GetPropertyTypes(
        this CSharpProperty property, GeneratorOptions options)
    {
        return (
            ReturnType: options.IsWebAssembly ? property.MappedTypeName : $"ValueTask<{property.MappedTypeName}>",
            BareType: property.MappedTypeName);
    }
}
