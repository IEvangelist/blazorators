// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class MethodSignature : Declaration, ISignatureDeclaration, ITypeElement, IFunctionLikeDeclaration
{
    internal MethodSignature() => ((INode)this).Kind = CommentKind.MethodSignature;

    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object ITypeElement.TypeElementBrand { get; set; } = default!;
    QuestionToken ITypeElement.QuestionToken { get; set; } = default!;
    QuestionToken IFunctionLikeDeclaration.QuestionToken { get; set; } = default!;
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand { get; set; } = default!;
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken { get; set; } = default!;
    IBlockOrExpression IFunctionLikeDeclaration.Body { get; set; } = default!;
}
