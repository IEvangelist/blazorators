// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class UnionOrIntersectionType : TsType, IUnionOrIntersectionType
{
    internal TsType[] Types { get; set; }
    internal SymbolTable PropertyCache { get; set; }
    internal Symbol[] ResolvedProperties { get; set; }
    internal IndexType ResolvedIndexType { get; set; }
    internal TsType ResolvedBaseConstraint { get; set; }
    internal bool CouldContainTypeVariables { get; set; }
}