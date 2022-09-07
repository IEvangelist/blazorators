// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IDeclaration : INode
{
    internal object DeclarationBrand { get; set; }
    internal INode? Name { get; set; }
}
