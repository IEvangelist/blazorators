// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class MappedType : ObjectType
{
    public MappedTypeNode? Declaration { get; set; }
    public TypeParameter? TypeParameter { get; set; }
    public TsType? ConstraintType { get; set; }
    public TsType? TemplateType { get; set; }
    public TsType? ModifiersType { get; set; }
    public TypeMapper? Mapper { get; set; }
}