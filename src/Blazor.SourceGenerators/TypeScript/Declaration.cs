// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Declaration : Node, IDeclaration
{
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}
