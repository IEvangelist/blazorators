// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeParameter : TypeVariable
{
    public TsType? Constraint { get; set; }
    public TsType? Default { get; set; }
    public TypeParameter? Target { get; set; }
    public TypeMapper? Mapper { get; set; }
    public bool IsThisType { get; set; }
    public TsType? ResolvedDefaultType { get; set; }
}