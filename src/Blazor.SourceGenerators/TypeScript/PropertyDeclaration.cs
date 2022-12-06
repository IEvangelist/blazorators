// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class PropertyDeclaration : ClassElement, IVariableLikeDeclaration
{
    public PropertyDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.PropertyDeclaration;

    public QuestionToken QuestionToken { get; set; }
    public ITypeNode Type { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
}
