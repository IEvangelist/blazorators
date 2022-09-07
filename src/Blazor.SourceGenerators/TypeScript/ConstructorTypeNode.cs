// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ConstructorTypeNode : Node, ITypeNode, IFunctionOrConstructorTypeNode
{
    internal ConstructorTypeNode() => ((INode)this).Kind = CommentKind.ConstructorType;

    object ITypeNode.TypeNodeBrand { get; set; } = default!;
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}