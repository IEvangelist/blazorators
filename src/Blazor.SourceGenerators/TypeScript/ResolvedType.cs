// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ResolvedType : TsType, IObjectType, IUnionOrIntersectionType
{
    internal SymbolTable Members { get; set; }
    internal Symbol[] Properties { get; set; }
    internal Signature[] CallSignatures { get; set; }
    internal Signature[] ConstructSignatures { get; set; }
    internal IndexInfo StringIndexInfo { get; set; }
    internal IndexInfo NumberIndexInfo { get; set; }
    internal ObjectFlags ObjectFlags { get; set; }
    internal TsType[] Types { get; set; }
    internal SymbolTable PropertyCache { get; set; }
    internal Symbol[] ResolvedProperties { get; set; }
    internal IndexType ResolvedIndexType { get; set; }
    internal TsType ResolvedBaseConstraint { get; set; }
    internal bool CouldContainTypeVariables { get; set; }
}