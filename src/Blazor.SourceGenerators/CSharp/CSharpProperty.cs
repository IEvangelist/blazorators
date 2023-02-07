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
            var mappedTypeName = TypeMap.PrimitiveTypes[RawTypeName];
            if (mappedTypeName == RawTypeName)
            {
                if (IsArray)
                {
                    mappedTypeName = mappedTypeName
                        .Replace("[]", "")
                        .Replace("ReadonlyArray<", "")
                        .Replace(">", "");
                }

                if (IsNullable)
                {
                    mappedTypeName = mappedTypeName
                        .Replace("| null", "");
                }
            }

            return mappedTypeName;
        }
    }

    public bool IsIndexer => RawName.StartsWith("[") && RawName.EndsWith("]");

    public bool IsArray => RawTypeName.EndsWith("[]") ||
        (RawTypeName.StartsWith("ReadonlyArray<") && RawTypeName.EndsWith(">"));
}
