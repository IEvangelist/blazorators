// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IVariableLikeDeclaration : IDeclaration
{
    IPropertyName PropertyName { get; set; }
    DotDotDotToken DotDotDotToken { get; set; }
    QuestionToken QuestionToken { get; set; }
    ITypeNode Type { get; set; }
    IExpression Initializer { get; set; }
}
