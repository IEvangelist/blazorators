// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeParameter : TypeVariable
{
    public TypeScriptType Constraint { get; set; }
    public TypeScriptType Default { get; set; }
    public TypeParameter Target { get; set; }
    public TypeMapper Mapper { get; set; }
    public bool IsThisType { get; set; }
    public TypeScriptType ResolvedDefaultType { get; set; }
}