// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IInterfaceType : IObjectType
{
    TypeParameter[] TypeParameters { get; set; }
    TypeParameter[] OuterTypeParameters { get; set; }
    TypeParameter[] LocalTypeParameters { get; set; }
    TypeParameter ThisType { get; set; }
    TypeScriptType ResolvedBaseConstructorType { get; set; }
    IBaseType[] ResolvedBaseTypes { get; set; }
}