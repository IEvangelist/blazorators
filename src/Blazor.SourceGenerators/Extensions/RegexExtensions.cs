// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class RegexExtensions
{
    internal static string? GetMatchGroupValue(
        this Regex regex, string input, string groupName) =>
        regex.Match(input) is Match match
            ? match.GetGroupValue(groupName)
            : default;

    internal static string? GetGroupValue(
        this Match match, string groupName) =>
        match switch
        {
            { Success: true } => match.Groups?[groupName]?.Value,
            _ => default
        };
}
