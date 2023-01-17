// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IUnionOrIntersectionType : IType
{
    TypeScriptType[] Types { get; set; }
    SymbolTable PropertyCache { get; set; }
    Symbol[] ResolvedProperties { get; set; }
    IndexType ResolvedIndexType { get; set; }
    TypeScriptType ResolvedBaseConstraint { get; set; }
    bool CouldContainTypeVariables { get; set; }
}