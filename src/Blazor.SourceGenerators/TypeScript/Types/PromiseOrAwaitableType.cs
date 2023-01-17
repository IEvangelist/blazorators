// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PromiseOrAwaitableType : TypeScriptType, IObjectType, IUnionType
{
    public TypeScriptType PromiseTypeOfPromiseConstructor { get; set; }
    public TypeScriptType PromisedTypeOfPromise { get; set; }
    public TypeScriptType AwaitedTypeOfType { get; set; }
    public ObjectFlags ObjectFlags { get; set; }
    public TypeScriptType[] Types { get; set; }
    public SymbolTable PropertyCache { get; set; }
    public Symbol[] ResolvedProperties { get; set; }
    public IndexType ResolvedIndexType { get; set; }
    public TypeScriptType ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}