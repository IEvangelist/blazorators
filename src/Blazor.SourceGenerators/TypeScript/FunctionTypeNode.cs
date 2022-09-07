// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FunctionTypeNode : Node, ITypeNode, IFunctionOrConstructorTypeNode
{
    internal FunctionTypeNode() => ((INode)this).Kind = CommentKind.FunctionType;

    object ITypeNode.TypeNodeBrand { get; set; } = default!;
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}