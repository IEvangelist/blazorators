// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IDeclaration : INode
{
    public object DeclarationBrand { get; set; }
    public INode? Name { get; set; }
}
