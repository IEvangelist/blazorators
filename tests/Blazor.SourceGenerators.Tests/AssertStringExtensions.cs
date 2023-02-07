// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Blazor.SourceGenerators.Tests;

static class AssertStringExtensions
{
    internal static string NormalizeNewlines(this string value) =>
        Regex.Replace(value, @"\r\n|\n\r|\n|\r", "\r\n");
}
