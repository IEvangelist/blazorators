// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ResolvedType : TsType, IObjectType, IUnionOrIntersectionType
{
    public SymbolTable? Members { get; set; }
    public Symbol[]? Properties { get; set; }
    public Signature[]? CallSignatures { get; set; }
    public Signature[]? ConstructSignatures { get; set; }
    public IndexInfo? StringIndexInfo { get; set; }
    public IndexInfo? NumberIndexInfo { get; set; }
    public ObjectFlags ObjectFlags { get; set; }
    public TsType[]? Types { get; set; }
    public SymbolTable? PropertyCache { get; set; }
    public Symbol[]? ResolvedProperties { get; set; }
    public IndexType? ResolvedIndexType { get; set; }
    public TsType? ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}