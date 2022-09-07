// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class MappedType : ObjectType
{
    internal MappedTypeNode Declaration { get; set; }
    internal TypeParameter TypeParameter { get; set; }
    internal TsType ConstraintType { get; set; }
    internal TsType TemplateType { get; set; }
    internal TsType ModifiersType { get; set; }
    internal TypeMapper Mapper { get; set; }
}