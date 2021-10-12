// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter;

static class StringExtensions
{
    static readonly InterfaceConverter s_interfaceConverter = new();

    internal static string? AsCSharpSourceText(this string? typeScriptDefinitionText)
    {
        if (typeScriptDefinitionText is null)
        {
            return default!;
        }

        if (typeScriptDefinitionText.StartsWith("interface"))
        {
            return s_interfaceConverter.ToCSharpSourceText(typeScriptDefinitionText);
        }

        return default!;

        // Type conversions handled:
        // - interface
        // - type
        //
        // Type conversions intentionally not handled:
        // - any type or interface with "Element" in the name
        // - declare var
        // - declare function
    }

    internal static string CapitalizeFirstLetter(this string name) =>
        $"{char.ToUpper(name[0])}{name[1..]}";
}

