// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IVariableLikeDeclaration : IDeclaration
{
    IPropertyName PropertyName { get; set; }
    DotDotDotToken DotDotDotToken { get; set; }
    QuestionToken QuestionToken { get; set; }
    ITypeNode Type { get; set; }
    IExpression Initializer { get; set; }
}