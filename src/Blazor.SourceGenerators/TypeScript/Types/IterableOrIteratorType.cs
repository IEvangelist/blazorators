// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IterableOrIteratorType : TypeScriptType, IObjectType, IUnionType
{
    public TypeScriptType IteratedTypeOfIterable { get; set; }
    public TypeScriptType IteratedTypeOfIterator { get; set; }
    public TypeScriptType IteratedTypeOfAsyncIterable { get; set; }
    public TypeScriptType IteratedTypeOfAsyncIterator { get; set; }
    public ObjectFlags ObjectFlags { get; set; }
    public TypeScriptType[] Types { get; set; }
    public SymbolTable PropertyCache { get; set; }
    public Symbol[] ResolvedProperties { get; set; }
    public IndexType ResolvedIndexType { get; set; }
    public TypeScriptType ResolvedBaseConstraint { get; set; }
    public bool CouldContainTypeVariables { get; set; }
}