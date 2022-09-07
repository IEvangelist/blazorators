// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class InterfaceType : ObjectType, IInterfaceType
{
    TypeParameter[] IInterfaceType.TypeParameters { get; set; } = default!;
    TypeParameter[] IInterfaceType.OuterTypeParameters { get; set; } = default!;
    TypeParameter[] IInterfaceType.LocalTypeParameters { get; set; } = default!;
    TypeParameter IInterfaceType.ThisType { get; set; } = default!;
    TsType IInterfaceType.ResolvedBaseConstructorType { get; set; } = default!;
    IBaseType[] IInterfaceType.ResolvedBaseTypes { get; set; } = default!;
}