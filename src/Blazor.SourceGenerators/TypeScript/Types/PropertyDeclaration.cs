// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PropertyDeclaration : ClassElement, IVariableLikeDeclaration
{
    public PropertyDeclaration()
    {
        Kind = TypeScriptSyntaxKind.PropertyDeclaration;
    }

    public QuestionToken QuestionToken { get; set; }
    public ITypeNode Type { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
}