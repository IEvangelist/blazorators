// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Blazor.SourceGenerators.Tests;

static partial class AssertStringExtensions
{
    internal static string NormalizeNewlines(this string value) =>
        VariousLineEndingsRegex().Replace(value, "\r\n");
    
    [GeneratedRegex(@"\r\n|\n\r|\n|\r")]
    private static partial Regex VariousLineEndingsRegex();
}
