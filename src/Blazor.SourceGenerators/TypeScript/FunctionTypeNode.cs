// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FunctionTypeNode : Node, ITypeNode, IFunctionOrConstructorTypeNode
{
    public FunctionTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.FunctionType;

    object ITypeNode.TypeNodeBrand? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}