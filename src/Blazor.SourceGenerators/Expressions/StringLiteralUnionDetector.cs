// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Expressions;

/// <summary>
/// Detects TypeScript string-literal union shapes (e.g.
/// <c>"a" | "b" | "c"</c>) and converts each raw string value into a
/// valid C# enum member identifier. This is the foundation for the
/// (not-yet-emitted) Phase B3 enum projection: callers can ask whether
/// a TypeScript type alias is a string-literal union and obtain the
/// distinct ordered list of raw members without changing any
/// consumer-visible generated output.
/// </summary>
internal static class StringLiteralUnionDetector
{
    // ^ and $ in multiline mode let us accept bodies that span several
    // physical lines (some `.d.ts` corpora pretty-print long unions
    // across multiple lines). Singleline so `.` crosses line breaks
    // inside the optional whitespace runs.
    private static readonly Regex s_stringLiteralUnionBodyRegex =
        new(
            @"^\s*""(?:[^""\\]|\\.)*""(?:\s*\|\s*""(?:[^""\\]|\\.)*"")*\s*;?\s*$",
            RegexOptions.Compiled | RegexOptions.Singleline |
            RegexOptions.CultureInvariant);

    private static readonly Regex s_stringLiteralTokenRegex =
        new(
            @"""(?<value>(?:[^""\\]|\\.)*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Separators we honor when PascalCasing a raw value. Hyphen,
    // underscore, dot, space, and forward slash cover every
    // segmentation pattern we've seen in `lib.dom.d.ts` (kebab-case,
    // snake_case, dotted media types, "afterbegin" style, etc.).
    private static readonly char[] s_pascalSeparators =
        new[] { '-', '_', '.', ' ', '/' };

    /// <summary>
    /// Tries to parse the supplied alias body (the text on the right
    /// hand side of <c>type X = ...</c>, with the optional trailing
    /// semicolon either stripped or retained) as a string-literal
    /// union. On success, <paramref name="rawMembers"/> is the distinct
    /// ordered list of raw string values (duplicates preserve the
    /// first occurrence). On failure, the list is empty.
    /// </summary>
    public static bool TryParse(string? body, out IReadOnlyList<string> rawMembers)
    {
        if (body is null || string.IsNullOrWhiteSpace(body))
        {
            rawMembers = Array.Empty<string>();
            return false;
        }

        if (!s_stringLiteralUnionBodyRegex.IsMatch(body))
        {
            rawMembers = Array.Empty<string>();
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        foreach (Match m in s_stringLiteralTokenRegex.Matches(body))
        {
            var raw = m.Groups["value"].Value;
            if (seen.Add(raw))
            {
                ordered.Add(raw);
            }
        }

        if (ordered.Count == 0)
        {
            rawMembers = Array.Empty<string>();
            return false;
        }

        rawMembers = ordered;
        return true;
    }

    /// <summary>
    /// Converts a raw TypeScript string-literal value (e.g.
    /// <c>"data-source"</c>) into a C# enum member identifier
    /// (<c>"DataSource"</c>). Returns <see langword="null"/> when no
    /// valid identifier can be constructed — empty input, whitespace
    /// only, or a value that consists entirely of separator characters.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item>Split the raw value on any of <c>- _ . " " /</c>.</item>
    /// <item>Drop empty segments produced by leading or repeated
    /// separators (e.g. <c>"-webkit-blah"</c> becomes
    /// <c>WebkitBlah</c>, not <c>Webkit</c>).</item>
    /// <item>Pascal-case each remaining segment (first character upper
    /// invariant, rest unchanged) and concatenate.</item>
    /// <item>If the resulting identifier starts with an ASCII digit,
    /// prepend an underscore so the identifier is legal C# (e.g.
    /// <c>"2d"</c> becomes <c>_2d</c>).</item>
    /// </list>
    /// PascalCasing naturally resolves C# keyword collisions for
    /// lowercase TS values such as <c>"class"</c>, <c>"new"</c>, and
    /// <c>"this"</c> since the resulting <c>Class</c>, <c>New</c>, and
    /// <c>This</c> are not reserved.
    /// </remarks>
    public static string? ToEnumMemberName(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw.Length == 0)
        {
            return null;
        }

        var segments = raw.Split(s_pascalSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                continue;
            }

            // Upper-invariant the leading character; preserve the rest
            // verbatim (so existing camelCase segments like "kebab" in
            // "data-kebab" become "Kebab" while existing PascalCase
            // segments like "URL" in "base-URL" remain "URL").
            builder.Append(char.ToUpperInvariant(segment[0]));
            if (segment.Length > 1)
            {
                builder.Append(segment, 1, segment.Length - 1);
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
