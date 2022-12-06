// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocFunctionType : Node, IJsDocType, ISignatureDeclaration
{
    public JsDocFunctionType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocFunctionType;

    object IJsDocType.JsDocTypeBrand? { get; set; }
    object ITypeNode.TypeNodeBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
}