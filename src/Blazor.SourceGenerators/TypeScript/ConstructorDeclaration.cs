// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ConstructorDeclaration : Declaration, IFunctionLikeDeclaration, IClassElement
{
    internal ConstructorDeclaration() => ((INode)this).Kind = CommentKind.Constructor;

    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand { get; set; } = default!;
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken { get; set; } = default!;
    QuestionToken IFunctionLikeDeclaration.QuestionToken { get; set; } = default!;
    IBlockOrExpression IFunctionLikeDeclaration.Body { get; set; } = default!;
    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object IClassElement.ClassElementBrand { get; set; } = default!;
}
