// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter.Extensions;

static class StringExtensions
{
    internal static string CapitalizeFirstLetter(this string name) =>
        $"{char.ToUpper(name[0])}{name[1..]}";

    internal static string LowerCaseFirstLetter(this string name) =>
        $"{char.ToLower(name[0])}{name[1..]}";
}

