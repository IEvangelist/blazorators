// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class MethodSignature : Declaration, ISignatureDeclaration, ITypeElement, IFunctionLikeDeclaration
{
    public MethodSignature() => ((INode)this).Kind = TypeScriptSyntaxKind.MethodSignature;

    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters? { get; set; }
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters? { get; set; }
    ITypeNode ISignatureDeclaration.Type? { get; set; }
    object ITypeElement.TypeElementBrand? { get; set; }
    QuestionToken ITypeElement.QuestionToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
}
