// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PromiseOrAwaitableType : TsType, IObjectType, IUnionType
{
    public TsType? PromiseTypeOfPromiseConstructor { get; set; }
    public TsType? PromisedTypeOfPromise { get; set; }
    public TsType? AwaitedTypeOfType { get; set; }
    public ObjectFlags ObjectFlags { get; set; }
    public TsType[]? Types { get; set; }
    public SymbolTable? PropertyCache { get; set; }
    public Symbol[]? ResolvedProperties { get; set; }
    public IndexType? ResolvedIndexType { get; set; }
    public TsType? ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}