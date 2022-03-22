// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class StringExtensions
{
    internal static string CapitalizeFirstLetter(this string name) =>
        $"{char.ToUpper(name[0])}{name.Substring(1, name.Length - 1)}";

    internal static string LowerCaseFirstLetter(this string name) =>
        $"{char.ToLower(name[0])}{name.Substring(1, name.Length - 1)}";

    internal static string ToGeneratedFileName(this string name) => $"{name}.g.cs";

    internal static string ToImplementationName(this string implementation, bool isService = true)
    {
        var impl = (implementation.Contains(".")
            ? implementation.Substring(implementation.LastIndexOf(".") + 1)
            : implementation).CapitalizeFirstLetter();

        return $"{impl}{(isService ? "Service" : "")}";
    }

    internal static string ToInterfaceName(this string typeName, bool isService = true)
    {
        var type = (typeName.Contains(".")
            ? typeName.Substring(typeName.LastIndexOf(".") + 1)
            : typeName).CapitalizeFirstLetter();

        return $"I{type}{(isService ? "Service" : "")}";
    }
}
