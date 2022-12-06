// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class UnionOrIntersectionType : TsType, IUnionOrIntersectionType
{
    public TsType[]? Types { get; set; }
    public SymbolTable? PropertyCache { get; set; }
    public Symbol[]? ResolvedProperties { get; set; }
    public IndexType? ResolvedIndexType { get; set; }
    public TsType? ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}