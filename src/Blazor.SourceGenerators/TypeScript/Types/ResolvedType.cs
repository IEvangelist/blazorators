// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ResolvedType : TypeScriptType, IObjectType, IUnionOrIntersectionType
{
    public SymbolTable Members { get; set; }
    public Symbol[] Properties { get; set; }
    public Signature[] CallSignatures { get; set; }
    public Signature[] ConstructSignatures { get; set; }
    public IndexInfo StringIndexInfo { get; set; }
    public IndexInfo NumberIndexInfo { get; set; }
    public ObjectFlags ObjectFlags { get; set; }
    public TypeScriptType[] Types { get; set; }
    public SymbolTable PropertyCache { get; set; }
    public Symbol[] ResolvedProperties { get; set; }
    public IndexType ResolvedIndexType { get; set; }
    public TypeScriptType ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}