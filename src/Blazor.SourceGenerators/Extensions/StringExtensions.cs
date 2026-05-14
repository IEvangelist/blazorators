// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class StringExtensions
{
    internal static string CapitalizeFirstLetter(this string name) =>
        string.IsNullOrEmpty(name)
            ? name
            : $"{char.ToUpper(name[0])}{name.Substring(1)}";

    internal static string LowerCaseFirstLetter(this string name) =>
        string.IsNullOrEmpty(name)
            ? name
            : $"{char.ToLower(name[0])}{name.Substring(1)}";

    internal static string ToGeneratedFileName(this string name) => $"{name}.g.cs";

    internal static string ToImplementationName(this string implementation, bool isService = true)
    {
        var impl = ExtractLastSegment(implementation).CapitalizeFirstLetter();

        return $"{impl}{(isService ? "Service" : "")}";
    }

    internal static string ToInterfaceName(this string implementation, bool isService = true)
    {
        var type = ExtractLastSegment(implementation).CapitalizeFirstLetter();

        return $"I{type}{(isService ? "Service" : "")}";
    }

    private static string ExtractLastSegment(string implementation)
    {
        if (string.IsNullOrEmpty(implementation))
        {
            return implementation;
        }

        var lastDot = implementation.LastIndexOf('.');
        return lastDot >= 0 && lastDot < implementation.Length - 1
            ? implementation.Substring(lastDot + 1)
            : implementation;
    }
}
