// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class MappedType : ObjectType
{
    public MappedTypeNode Declaration { get; set; }
    public TypeParameter TypeParameter { get; set; }
    public TypeScriptType ConstraintType { get; set; }
    public TypeScriptType TemplateType { get; set; }
    public TypeScriptType ModifiersType { get; set; }
    public TypeMapper Mapper { get; set; }
}