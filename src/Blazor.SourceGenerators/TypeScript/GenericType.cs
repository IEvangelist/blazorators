// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class GenericType : ObjectType, IInterfaceType, ITypeReference
{
    TypeParameter[] IInterfaceType.TypeParameters? { get; set; }
    TypeParameter[] IInterfaceType.OuterTypeParameters? { get; set; }
    TypeParameter[] IInterfaceType.LocalTypeParameters? { get; set; }
    TypeParameter IInterfaceType.ThisType? { get; set; }
    TsType IInterfaceType.ResolvedBaseConstructorType? { get; set; }
    IBaseType[] IInterfaceType.ResolvedBaseTypes? { get; set; }
    GenericType ITypeReference.Target? { get; set; }
    TsType[] ITypeReference.TypeArguments? { get; set; }
}