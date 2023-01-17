// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class Declaration : Node, IDeclaration
{
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
}