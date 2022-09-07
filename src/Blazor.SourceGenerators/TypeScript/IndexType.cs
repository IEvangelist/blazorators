// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class IndexType : TsType
{
    internal TsType Type { get; set; } // TypeVariable | UnionOrIntersectionType
}