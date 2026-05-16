// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

static class StringExtensions
{
    /// <summary>
    /// C# reserved keywords that cannot appear as bare identifiers.
    /// Used by <see cref="EscapeCSharpKeyword"/> to prepend a verbatim
    /// <c>@</c> prefix when a TypeScript identifier (parameter or local
    /// name) collides with a keyword. The DOM has several real-world
    /// hits: <c>Document.createElementNS(namespace: string, ...)</c>,
    /// <c>HTMLLinkElement.event</c>, <c>HTMLTrackElement.default</c>,
    /// <c>EcdhKeyDeriveParams.public</c>, etc. Without escaping the
    /// generator emitted bare identifiers that did not parse as C#.
    ///
    /// Includes only the *reserved* set; contextual keywords (<c>var</c>,
    /// <c>nameof</c>, <c>where</c>, etc.) remain valid identifiers and
    /// do not need escaping.
    /// </summary>
    private static readonly HashSet<string> s_csharpReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
        "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while",
    };

    /// <summary>
    /// Returns the input with a leading <c>@</c> when it collides with
    /// a C# reserved keyword (see <see cref="s_csharpReservedKeywords"/>).
    /// Safe to call on any string: non-keyword inputs and inputs that
    /// already start with <c>@</c> are returned unchanged.
    /// </summary>
    internal static string EscapeCSharpKeyword(this string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] == '@')
        {
            return name;
        }

        return s_csharpReservedKeywords.Contains(name) ? $"@{name}" : name;
    }

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
