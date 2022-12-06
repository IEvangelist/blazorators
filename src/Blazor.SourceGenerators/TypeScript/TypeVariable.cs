// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeVariable : TsType
{
    public TsType? ResolvedBaseConstraint { get; set; }
    public IndexType? ResolvedIndexType { get; set; }
}