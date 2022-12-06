// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ConstructorTypeNode : Node, ITypeNode, IFunctionOrConstructorTypeNode
{
    public ConstructorTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.ConstructorType;

    object ITypeNode.TypeNodeBrand? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}