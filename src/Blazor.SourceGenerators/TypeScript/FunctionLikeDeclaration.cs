// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class FunctionLikeDeclaration : SignatureDeclaration, IFunctionLikeDeclaration
{
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
}
