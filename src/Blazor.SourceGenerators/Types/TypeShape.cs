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
    // TS uses ` | undefined` semantically the same way the rest of
    // the runtime treats `| null`: an optional / missing value. The
    // C# binding represents both as a nullable reference / value
    // type (`T?`) -- there's no observable difference once the
    // value crosses JS interop. We collapse both forms here so the
    // rest of the pipeline (primitive map, array element resolution,
    // property mapped-type, parameter parsing) doesn't need parallel
    // code paths.
    private const string UndefinedClause = " | undefined";

    internal static string StripNullClause(string rawTypeName)
    {
        if (rawTypeName.EndsWith(NullClause, StringComparison.Ordinal))
        {
            return rawTypeName.Substring(0, rawTypeName.Length - NullClause.Length);
        }

        if (rawTypeName.EndsWith(UndefinedClause, StringComparison.Ordinal))
        {
            return rawTypeName.Substring(0, rawTypeName.Length - UndefinedClause.Length);
        }

        return rawTypeName;
    }

    /// <summary>
    /// True when the input ends in either <c> | null</c> or
    /// <c> | undefined</c>. Used by callers that need to detect
    /// nullability without also stripping the clause.
    /// </summary>
    internal static bool HasNullClause(string rawTypeName) =>
        rawTypeName.EndsWith(NullClause, StringComparison.Ordinal) ||
        rawTypeName.EndsWith(UndefinedClause, StringComparison.Ordinal);

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
