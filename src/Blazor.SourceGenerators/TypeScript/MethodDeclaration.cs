// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal sealed class MethodDeclaration : Declaration,
    IFunctionLikeDeclaration,
    IClassElement,
    IObjectLiteralElement,
    IObjectLiteralElementLike
{
    internal MethodDeclaration() => ((INode)this).Kind = CommentKind.MethodDeclaration;

    object IClassElement.ClassElementBrand { get; set; } = default!;
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand { get; set; } = default!;
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken { get; set; } = default!;
    QuestionToken IFunctionLikeDeclaration.QuestionToken { get; set; } = default!;
    IBlockOrExpression IFunctionLikeDeclaration.Body { get; set; } = default!;
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object IObjectLiteralElement.ObjectLiteralBrandBrand { get; set; } = default!;
}
