// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace TypeScript.TypeConverter.Extensions;

static class RegexExtensions
{
    internal static string? GetMatchGroupValue(this Regex regex, string input, string groupName)
    {
        var match = regex.Match(input);
        if (match is null)
        {
            return default!;
        }

        if (match is { Success: true } and { Groups: { Count: > 0 } })
        {
            return match.Groups[groupName]?.Value;
        }

        return default!;
    }
}
