// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class InterfaceType : ObjectType, IInterfaceType
{
    TypeParameter[] IInterfaceType.TypeParameters? { get; set; }
    TypeParameter[] IInterfaceType.OuterTypeParameters? { get; set; }
    TypeParameter[] IInterfaceType.LocalTypeParameters? { get; set; }
    TypeParameter IInterfaceType.ThisType? { get; set; }
    TsType IInterfaceType.ResolvedBaseConstructorType? { get; set; }
    IBaseType[] IInterfaceType.ResolvedBaseTypes? { get; set; }
}