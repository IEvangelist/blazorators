// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IFunctionLikeDeclaration : ISignatureDeclaration
{
    object? FunctionLikeDeclarationBrand { get; set; }
    AsteriskToken? AsteriskToken { get; set; }
    QuestionToken? QuestionToken { get; set; }
    IBlockOrExpression? Body { get; set; }
}
