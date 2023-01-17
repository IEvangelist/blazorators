// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IDeclaration : INode
{
    object DeclarationBrand { get; set; }
    INode Name { get; set; }
}