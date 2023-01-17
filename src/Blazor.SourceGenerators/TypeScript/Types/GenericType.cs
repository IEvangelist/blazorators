// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class GenericType : ObjectType, IInterfaceType, ITypeReference
{
    public Map<TypeReference> Instantiations { get; set; }
    public TypeParameter[] TypeParameters { get; set; }
    public TypeParameter[] OuterTypeParameters { get; set; }
    public TypeParameter[] LocalTypeParameters { get; set; }
    public TypeParameter ThisType { get; set; }
    public TypeScriptType ResolvedBaseConstructorType { get; set; }
    public IBaseType[] ResolvedBaseTypes { get; set; }
    public GenericType Target { get; set; }
    public TypeScriptType[] TypeArguments { get; set; }
}