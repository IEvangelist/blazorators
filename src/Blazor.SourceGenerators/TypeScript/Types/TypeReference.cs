// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeReference : ObjectType, ITypeReference
{
    public GenericType Target { get; set; }
    public TypeScriptType[] TypeArguments { get; set; }
}