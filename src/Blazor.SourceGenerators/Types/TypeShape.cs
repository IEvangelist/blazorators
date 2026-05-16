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
        // Loop so layered clauses (`T | null | undefined`, rare in DOM
        // but legal TS and present in a few WebGL extension declarations)
        // collapse to a single bare `T`. Both forms are semantically
        // identical from the C# side -- repeated peeling avoids
        // partial-strip artefacts leaking into the emitted signature.
        var current = rawTypeName;
        while (true)
        {
            if (current.EndsWith(NullClause, StringComparison.Ordinal))
            {
                current = current.Substring(0, current.Length - NullClause.Length);
                continue;
            }

            if (current.EndsWith(UndefinedClause, StringComparison.Ordinal))
            {
                current = current.Substring(0, current.Length - UndefinedClause.Length);
                continue;
            }

            return current;
        }
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

    /// <summary>
    /// Detects the TypeScript <c>Record&lt;K, V&gt;</c> utility-type
    /// shape and splits its two generic arguments. Used by the
    /// property-mapping code and the parameter parser to emit a C#
    /// <c>Dictionary&lt;TKey, TValue&gt;</c> at the call site instead
    /// of leaking the raw <c>Record&lt;...&gt;</c> token into generated
    /// source. Real DOM hits include
    /// <c>RTCStats.parameterData: Record&lt;string, number&gt;</c>,
    /// <c>PushSubscriptionJSON.keys: Record&lt;string, string&gt;</c>,
    /// and the <c>HeadersInit</c> alias used by <c>fetch</c> /
    /// <c>Headers</c>.
    /// </summary>
    /// <remarks>
    /// The splitter walks the argument string with depth tracking so a
    /// nested generic value type (<c>Record&lt;string, Array&lt;number&gt;&gt;</c>)
    /// keeps its inner generics intact. A trailing <c>| null</c> /
    /// <c>| undefined</c> clause is stripped before matching -- the
    /// outer caller still observes nullability through the property's
    /// own nullable flag.
    /// </remarks>
    internal static bool TryGetRecordTypeArguments(
        string rawTypeName,
        out string keyType,
        out string valueType)
    {
        keyType = string.Empty;
        valueType = string.Empty;

        if (string.IsNullOrEmpty(rawTypeName))
        {
            return false;
        }

        const string prefix = "Record<";
        var stripped = StripNullClause(rawTypeName);

        if (!stripped.StartsWith(prefix, StringComparison.Ordinal) ||
            !stripped.EndsWith(">", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = stripped.Substring(prefix.Length, stripped.Length - prefix.Length - 1);
        var depth = 0;
        var splitIndex = -1;

        for (var i = 0; i < inner.Length; i++)
        {
            var ch = inner[i];
            if (ch == '<')
            {
                depth++;
            }
            else if (ch == '>')
            {
                depth--;
            }
            else if (ch == ',' && depth == 0)
            {
                splitIndex = i;
                break;
            }
        }

        if (splitIndex < 0)
        {
            return false;
        }

        keyType = inner.Substring(0, splitIndex).Trim();
        valueType = inner.Substring(splitIndex + 1).Trim();
        return keyType.Length > 0 && valueType.Length > 0;
    }
}
