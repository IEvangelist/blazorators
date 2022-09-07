// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class IterableOrIteratorType : TsType, IObjectType, IUnionType
{
    ObjectFlags IObjectType.ObjectFlags { get; set; }
    TsType[] IUnionOrIntersectionType.Types { get; set; } = default!;
    SymbolTable IUnionOrIntersectionType.PropertyCache { get; set; } = default!;
    Symbol[] IUnionOrIntersectionType.ResolvedProperties { get; set; } = default!;
    IndexType IUnionOrIntersectionType.ResolvedIndexType { get; set; } = default!;
    TsType IUnionOrIntersectionType.ResolvedBaseConstraint { get; set; } = default!;
    bool IUnionOrIntersectionType.CouldContainTypeVariables { get; set; } = default!;
}