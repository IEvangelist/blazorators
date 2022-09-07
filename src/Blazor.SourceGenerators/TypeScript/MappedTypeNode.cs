// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class MappedTypeNode : Node, ITypeNode, IDeclaration
{
    internal MappedTypeNode() => ((INode)this).Kind = CommentKind.MappedType;

    object ITypeNode.TypeNodeBrand { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}