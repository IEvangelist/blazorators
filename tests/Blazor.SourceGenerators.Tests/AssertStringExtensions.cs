// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.Tests;

static partial class AssertStringExtensions
{
    internal static string NormalizeNewlines(this string value) =>
        NewLineRegex().Replace(value, SourceBuilder.NewLine.ToString());

    [GeneratedRegex(@"\r\n|\n\r|\n|\r")]
    private static partial Regex NewLineRegex();
}
