// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IUnionOrIntersectionType : IType
{
    TsType[] Types { get; set; }
    SymbolTable PropertyCache { get; set; }
    Symbol[] ResolvedProperties { get; set; }
    IndexType ResolvedIndexType { get; set; }
    TsType ResolvedBaseConstraint { get; set; }
    bool CouldContainTypeVariables { get; set; }
}