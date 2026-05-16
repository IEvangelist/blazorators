// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Types;

/// <summary>
/// Shape predicates and accessors for TypeScript type-name strings.
/// Centralizes the logic used to detect array-of-T forms (<c>T[]</c>,
/// <c>Array&lt;T&gt;</c>, <c>ReadonlyArray&lt;T&gt;</c>) and strip a
/// trailing <c>| null</c> clause so callers can compose the result
/// with the C# primitive map.
/// </summary>
internal static class TypeShape
{
    private const string NullClause = " | null";

    internal static string StripNullClause(string rawTypeName) =>
        rawTypeName.EndsWith(NullClause, StringComparison.Ordinal)
            ? rawTypeName.Substring(0, rawTypeName.Length - NullClause.Length)
            : rawTypeName;

    internal static bool IsArrayShape(string rawTypeName) =>
        rawTypeName.EndsWith("[]", StringComparison.Ordinal) ||
        IsGenericArrayForm(rawTypeName, "ReadonlyArray<") ||
        IsGenericArrayForm(rawTypeName, "Array<");

    private static bool IsGenericArrayForm(string rawTypeName, string prefix) =>
        rawTypeName.StartsWith(prefix, StringComparison.Ordinal) &&
        rawTypeName.EndsWith(">", StringComparison.Ordinal);

    internal static bool TryGetArrayElementTypeName(string rawTypeName, out string elementTypeName)
    {
        // Strip a trailing ` | null` so the array-shape match still fires
        // on `T[] | null`, `Array<T> | null`, and `ReadonlyArray<T> | null`.
        var stripped = StripNullClause(rawTypeName);

        if (stripped.EndsWith("[]", StringComparison.Ordinal))
        {
            elementTypeName = stripped.Substring(0, stripped.Length - 2);
            return true;
        }

        if (TryExtractGenericArgument(stripped, "ReadonlyArray<", out elementTypeName))
        {
            return true;
        }

        if (TryExtractGenericArgument(stripped, "Array<", out elementTypeName))
        {
            return true;
        }

        elementTypeName = string.Empty;
        return false;
    }

    private static bool TryExtractGenericArgument(
        string rawTypeName,
        string prefix,
        out string argument)
    {
        if (IsGenericArrayForm(rawTypeName, prefix))
        {
            argument = rawTypeName.Substring(prefix.Length, rawTypeName.Length - prefix.Length - 1);
            return true;
        }

        argument = string.Empty;
        return false;
    }
}
