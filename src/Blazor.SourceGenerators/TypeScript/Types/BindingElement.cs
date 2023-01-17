// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class BindingElement : Declaration, IArrayBindingElement, IVariableLikeDeclaration
{
    public BindingElement()
    {
        Kind = TypeScriptSyntaxKind.BindingElement;
    }

    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
    public IExpression Initializer { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public ITypeNode Type { get; set; }
}