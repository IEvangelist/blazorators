// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface ITypeReference : IObjectType
{
    GenericType Target { get; set; }
    TypeScriptType[] TypeArguments { get; set; }
}