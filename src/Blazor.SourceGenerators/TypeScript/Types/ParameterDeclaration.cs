// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ParameterDeclaration : Declaration, IVariableLikeDeclaration
{
    public ParameterDeclaration()
    {
        Kind = TypeScriptSyntaxKind.Parameter;
    }

    public DotDotDotToken DotDotDotToken { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public ITypeNode Type { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
}