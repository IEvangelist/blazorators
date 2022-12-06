// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class BindingElement : Declaration, IArrayBindingElement, IVariableLikeDeclaration
{
    public BindingElement() => ((INode)this).Kind = TypeScriptSyntaxKind.BindingElement;

    IPropertyName IVariableLikeDeclaration.PropertyName? { get; set; }

    DotDotDotToken IVariableLikeDeclaration.DotDotDotToken? { get; set; }

    IExpression IVariableLikeDeclaration.Initializer? { get; set; }

    QuestionToken IVariableLikeDeclaration.QuestionToken? { get; set; }

    ITypeNode IVariableLikeDeclaration.Type? { get; set; }
}
