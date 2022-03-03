// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class EnumExtensions
{
    internal static TEnum ToEnum<TEnum>(this string value) where TEnum : struct
    {
        return Enum.TryParse(value, out TEnum enumValue)
            ? enumValue
            : default;
    }
}
