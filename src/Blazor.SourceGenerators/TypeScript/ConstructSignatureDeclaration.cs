// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ConstructSignatureDeclaration : Declaration, ISignatureDeclaration, ITypeElement
{
    public ConstructSignatureDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ConstructSignature;

    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object ITypeElement.TypeElementBrand? { get; set; }
    QuestionToken ITypeElement.QuestionToken? { get; set; }
}
