// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IndexedAccessType : TypeVariable
{
    public TypeScriptType ObjectType { get; set; }
    public TypeScriptType IndexType { get; set; }
    public TypeScriptType Constraint { get; set; }
}