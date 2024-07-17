// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Compiler;

internal static class Core
{
    private const char DirectorySeparator = '/';
    private const CharacterCode DirectorySeparatorCharCode = CharacterCode.Slash;

    internal static int BinarySearch(
        int[] array, int value, Func<int, int, int> comparer = null, int? offset = null)
    {
        if (array is null || array.Length == 0)
        {
            return -1;
        }
        var low = offset ?? 0;
        var high = array.Length - 1;
        comparer ??= (v1, v2) =>
        {
            if (v1 < v2) return -1;
            return v1 > v2 ? 1 : 0;
        };
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var midValue = array[middle];
            if (comparer(midValue, value) == 0)
            {
                return middle;
            }
            else if (comparer(midValue, value) > 0)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }
        return ~low;
    }

#pragma warning disable S125 // Sections of code should not be commented out
#pragma warning disable S1940 // Boolean checks should not be inverted

    internal static bool PositionIsSynthesized(int pos) =>
      // This is a fast way of testing the following conditions:
      //  pos is null || pos is null || isNaN(pos) || pos < 0;
      !(pos >= 0);

#pragma warning restore S1940 // Boolean checks should not be inverted
#pragma warning restore S125 // Sections of code should not be commented out

    internal static ScriptKind EnsureScriptKind(string fileName, ScriptKind scriptKind)
    {
        // Using scriptKind as a condition handles both:
        // - 'scriptKind' is unspecified and thus it is `null`
        // - 'scriptKind' is set and it is `Unknown` (0)
        // If the 'scriptKind' is 'null' or 'Unknown' then we attempt
        // to get the ScriptKind from the file name. If it cannot be resolved
        // from the file name then the default 'TS' script kind is returned.
        var sk = scriptKind != ScriptKind.Unknown ? scriptKind : GetScriptKindFromFileName(fileName);
        return sk != ScriptKind.Unknown ? sk : ScriptKind.Ts;
    }

    internal static ScriptKind GetScriptKindFromFileName(string fileName) =>
        Path.GetExtension(fileName)?.ToLower() switch
        {
            ".js" => ScriptKind.Js,
            ".jsx" => ScriptKind.Jsx,
            ".ts" => ScriptKind.Ts,
            ".tsx" => ScriptKind.Tsx,
            _ => ScriptKind.Unknown
        };

    internal static string NormalizePath(string path)
    {
        path = NormalizeSlashes(path);
        var rootLength = GetRootLength(path);
        var root = path.Substring(rootLength);
        var normalized = GetNormalizedParts(path, rootLength);
        if (normalized.Count != 0)
        {
            var joinedParts = $"{root}{string.Join(DirectorySeparator.ToString(), normalized)}";
            return PathEndsWithDirectorySeparator(path) ? joinedParts + DirectorySeparator : joinedParts;
        }
        else
        {
            return root;
        }
    }

    internal static string NormalizeSlashes(string path) =>
        Regex.Replace(path, "/\\/ g", "/");

    internal static int GetRootLength(string path)
    {
        if (path.CharCodeAt(0) is CharacterCode.Slash)
        {
            if (path.CharCodeAt(1) is not CharacterCode.Slash)
            {
                return 1;
            }
            var p1 = path.IndexOf("/", 2, StringComparison.Ordinal);
            if (p1 < 0)
            {
                return 2;
            }
            var p2 = path.IndexOf("/", p1 + 1, StringComparison.Ordinal);
            return p2 < 0 ? p1 + 1 : p2 + 1;
        }
        if (path.CharCodeAt(1) is CharacterCode.Colon)
        {
            return path.CharCodeAt(2) is CharacterCode.Slash ? 3 : 2;
        }
        if (path.LastIndexOf("file:///", 0, StringComparison.Ordinal) == 0)
        {
            return "file:///".Length;
        }
        var idx = path.IndexOf("://", StringComparison.Ordinal);
        return idx != -1 ? idx + "://".Length : 0;
    }

    internal static List<string> GetNormalizedParts(string normalizedSlashedPath, int rootLength)
    {
        var parts = normalizedSlashedPath.Substring(rootLength).Split(DirectorySeparator);
        List<string> normalized = [];
        foreach (var part in parts)
        {
            if (part != ".")
            {
                if (part == ".." && normalized.Count > 0 && LastOrUndefined(normalized) != "..")
                {
                    normalized.Pop();
                }
                else
                {
                    // A part may be an empty string (which is 'falsy') if the path had consecutive slashes,
                    // e.g. "path//file.ts".  Drop these before re-joining the parts.
                    if (part != null)
                    {
                        normalized.Add(part);
                    }
                }
            }
        }
        return normalized;
    }

    internal static T LastOrUndefined<T>(List<T> array) where T : class =>
        array != null && array.Count != 0
            ? array[array.Count - 1]
            : default;

    internal static bool PathEndsWithDirectorySeparator(string path) =>
        path.CharCodeAt(path.Length - 1) == DirectorySeparatorCharCode;

    internal static bool FileExtensionIs(string path, string extension) =>
        path.Length > extension.Length && EndsWith(path, extension);

    internal static bool EndsWith(string str, string suffix)
    {
        var expectedPos = str.Length - suffix.Length;
        return expectedPos >= 0 && str.IndexOf(
            suffix,
            expectedPos,
            StringComparison.Ordinal) == expectedPos;
    }

    internal static TypeScriptDiagnostic CreateFileDiagnostic(
        SourceFile file,
        int start,
        int length,
        DiagnosticMessage? message = null,
        params string[] args)
    {
        TypeScriptDiagnostic diagnostic = new()
        {
            File = file,
            Start = start,
            Length = length,
            MessageText = GetLocaleSpecificMessage(message, args),
            Category = message?.Category ?? DiagnosticCategory.Unknown,
            Code = message?.Code ?? 0,
        };
        return diagnostic;
    }

    internal static string GetLocaleSpecificMessage(
        DiagnosticMessage? message = null,
        params string[] args) =>
        "localizedDiagnosticMessages";
}