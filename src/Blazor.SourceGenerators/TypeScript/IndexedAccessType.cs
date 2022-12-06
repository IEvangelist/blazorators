// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class IndexedAccessType : TypeVariable
{
    public TsType? ObjectType { get; set; }
    public TsType? IndexType { get; set; }
    public TsType? Constraint { get; set; }
}