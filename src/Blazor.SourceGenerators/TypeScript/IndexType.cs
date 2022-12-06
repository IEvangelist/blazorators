// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class IndexType : TsType
{
    public TsType? Type { get; set; } // TypeVariable | UnionOrIntersectionType
}