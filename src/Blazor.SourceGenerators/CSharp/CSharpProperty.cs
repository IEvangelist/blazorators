// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// A record the represents various C# members, such as properties, delegates and events.
/// </summary>
internal record CSharpProperty(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    bool IsReadonly = false) : CSharpType(RawName, RawTypeName, IsNullable)
{
    public string MappedTypeName
    {
        get
        {
            var direct = TypeMap.PrimitiveTypes[RawTypeName];
            if (!string.Equals(direct, RawTypeName, StringComparison.Ordinal))
            {
                return direct;
            }

            if (TryGetArrayElementTypeName(RawTypeName, out var elementTypeName))
            {
                return TypeMap.PrimitiveTypes[elementTypeName];
            }

            if (IsNullable)
            {
                return RawTypeName.Replace("| null", "").TrimEnd();
            }

            return RawTypeName;
        }
    }

    public bool IsIndexer => RawName.StartsWith("[") && RawName.EndsWith("]");

    public bool IsArray =>
        RawTypeName.EndsWith("[]") ||
        IsGenericArrayForm(RawTypeName, "ReadonlyArray<") ||
        IsGenericArrayForm(RawTypeName, "Array<");

    private static bool IsGenericArrayForm(string rawTypeName, string prefix) =>
        rawTypeName.StartsWith(prefix, StringComparison.Ordinal) &&
        rawTypeName.EndsWith(">", StringComparison.Ordinal);

    private static bool TryGetArrayElementTypeName(string rawTypeName, out string elementTypeName)
    {
        if (rawTypeName.EndsWith("[]", StringComparison.Ordinal))
        {
            elementTypeName = rawTypeName.Substring(0, rawTypeName.Length - 2);
            return true;
        }

        if (TryExtractGenericArgument(rawTypeName, "ReadonlyArray<", out elementTypeName))
        {
            return true;
        }

        if (TryExtractGenericArgument(rawTypeName, "Array<", out elementTypeName))
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
