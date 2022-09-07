// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class GenericType : ObjectType, IInterfaceType, ITypeReference
{
    TypeParameter[] IInterfaceType.TypeParameters { get; set; } = default!;
    TypeParameter[] IInterfaceType.OuterTypeParameters { get; set; } = default!;
    TypeParameter[] IInterfaceType.LocalTypeParameters { get; set; } = default!;
    TypeParameter IInterfaceType.ThisType { get; set; } = default!;
    TsType IInterfaceType.ResolvedBaseConstructorType { get; set; } = default!;
    IBaseType[] IInterfaceType.ResolvedBaseTypes { get; set; } = default!;
    GenericType ITypeReference.Target { get; set; } = default!;
    TsType[] ITypeReference.TypeArguments { get; set; } = default!;
}