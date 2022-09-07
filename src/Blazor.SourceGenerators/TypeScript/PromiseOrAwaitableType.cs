// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PromiseOrAwaitableType : TsType, IObjectType, IUnionType
{
    internal TsType PromiseTypeOfPromiseConstructor { get; set; }
    internal TsType PromisedTypeOfPromise { get; set; }
    internal TsType AwaitedTypeOfType { get; set; }
    internal ObjectFlags ObjectFlags { get; set; }
    internal TsType[] Types { get; set; }
    internal SymbolTable PropertyCache { get; set; }
    internal Symbol[] ResolvedProperties { get; set; }
    internal IndexType ResolvedIndexType { get; set; }
    internal TsType ResolvedBaseConstraint { get; set; }
    internal bool CouldContainTypeVariables { get; set; }
}