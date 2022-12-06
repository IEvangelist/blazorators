// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript.Parser;

public static class StringExtensions
{
    public static CharacterCode CharCodeAt(this string str, int pos) =>
        (CharacterCode)str[pos];

    public static string SubString(this string str, int start, int? end = null) =>
        end is null ? str.Substring(start) : str.Substring(start, (int)end);

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
        return end <= start ? string.Empty : str.Substring(start, end);
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
