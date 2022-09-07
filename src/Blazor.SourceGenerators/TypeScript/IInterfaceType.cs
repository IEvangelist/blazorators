// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IInterfaceType : IObjectType
{
    internal TypeParameter[] TypeParameters { get; set; }
    internal TypeParameter[] OuterTypeParameters { get; set; }
    internal TypeParameter[] LocalTypeParameters { get; set; }
    internal TypeParameter ThisType { get; set; }
    internal TsType ResolvedBaseConstructorType { get; set; }
    internal IBaseType[] ResolvedBaseTypes { get; set; }
}