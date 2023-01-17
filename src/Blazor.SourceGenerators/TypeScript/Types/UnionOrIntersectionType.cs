// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class UnionOrIntersectionType : TypeScriptType, IUnionOrIntersectionType
{
    public TypeScriptType[] Types { get; set; }
    public SymbolTable PropertyCache { get; set; }
    public Symbol[] ResolvedProperties { get; set; }
    public IndexType ResolvedIndexType { get; set; }
    public TypeScriptType ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}