// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

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

    internal static string ToInterfaceName(this string implementation, bool isService = true)
    {
        var type = (implementation.Contains(".")
            ? implementation.Substring(implementation.LastIndexOf(".") + 1)
            : implementation).CapitalizeFirstLetter();

        return $"I{type}{(isService ? "Service" : "")}";
    }

    public static CharacterCode CharCodeAt(this string str, int pos) =>
        (CharacterCode)str[pos];

    public static string SubString(this string str, int start, int? end = null) =>
        end is null ? str.Substring(start) : str.Substring(start, (int)end - start);

    public static string[] Exec(this Regex regex, string text) =>
        regex.Match(text).Captures.Cast<string>().ToArray();

    public static bool Test(this Regex r, string text) => r.IsMatch(text);

    public static void Pop<T>(this List<T> list) => list.RemoveAt(0);

    public static string Slice(this string str, int start, int end = int.MaxValue)
    {
        if (start < 0)
            start += str.Length;
        if (end < 0)
            end += str.Length;

        start = Math.Min(Math.Max(start, 0), str.Length);
        end = Math.Min(Math.Max(end, 0), str.Length);
        return end <= start ? string.Empty : str.Substring(start, end - 1);
    }

    public static string FromCharCode(params int[] codes)
    {
        var sb = new StringBuilder();
        foreach (var c in codes)
        {
            sb.Append((char)c);
        }
        return sb.ToString();
    }
}
