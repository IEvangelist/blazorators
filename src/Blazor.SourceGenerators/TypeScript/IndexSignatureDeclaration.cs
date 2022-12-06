// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class IndexSignatureDeclaration : Declaration,
    ISignatureDeclaration,
    IClassElement,
    ITypeElement
{
    public IndexSignatureDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.IndexSignature;

    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object IClassElement.ClassElementBrand? { get; set; }
    object ITypeElement.TypeElementBrand? { get; set; }
    QuestionToken ITypeElement.QuestionToken? { get; set; }
}
