// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IFunctionLikeDeclaration : ISignatureDeclaration
{
    internal object FunctionLikeDeclarationBrand { get; set; }
    internal AsteriskToken AsteriskToken { get; set; }
    internal QuestionToken QuestionToken { get; set; }
    internal IBlockOrExpression Body { get; set; }
}
