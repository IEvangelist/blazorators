// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class InterfaceType : ObjectType, IInterfaceType
{
    public TypeParameter[] TypeParameters { get; set; }
    public TypeParameter[] OuterTypeParameters { get; set; }
    public TypeParameter[] LocalTypeParameters { get; set; }
    public TypeParameter ThisType { get; set; }
    public TypeScriptType ResolvedBaseConstructorType { get; set; }
    public IBaseType[] ResolvedBaseTypes { get; set; }
}