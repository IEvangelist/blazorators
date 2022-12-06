// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class IterableOrIteratorType : TsType, IObjectType, IUnionType
{
    ObjectFlags IObjectType.ObjectFlags { get; set; }
    TsType[] IUnionOrIntersectionType.Types? { get; set; }
    SymbolTable IUnionOrIntersectionType.PropertyCache? { get; set; }
    Symbol[] IUnionOrIntersectionType.ResolvedProperties? { get; set; }
    IndexType IUnionOrIntersectionType.ResolvedIndexType? { get; set; }
    TsType IUnionOrIntersectionType.ResolvedBaseConstraint? { get; set; }
    bool IUnionOrIntersectionType.CouldContainTypeVariables? { get; set; }
}