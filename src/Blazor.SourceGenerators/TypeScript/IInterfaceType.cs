// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface IInterfaceType : IObjectType
{
    public TypeParameter[] TypeParameters { get; set; }
    public TypeParameter[] OuterTypeParameters { get; set; }
    public TypeParameter[] LocalTypeParameters { get; set; }
    public TypeParameter ThisType { get; set; }
    public TsType ResolvedBaseConstructorType { get; set; }
    public IBaseType[] ResolvedBaseTypes { get; set; }
}