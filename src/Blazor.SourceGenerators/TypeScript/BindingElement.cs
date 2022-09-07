// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class BindingElement : Declaration, IArrayBindingElement, IVariableLikeDeclaration
{
    internal BindingElement() => ((INode)this).Kind = CommentKind.BindingElement;

    IPropertyName IVariableLikeDeclaration.PropertyName { get; set; } = default!;

    DotDotDotToken IVariableLikeDeclaration.DotDotDotToken { get; set; } = default!;

    IExpression IVariableLikeDeclaration.Initializer { get; set; } = default!;

    QuestionToken IVariableLikeDeclaration.QuestionToken { get; set; } = default!;

    ITypeNode IVariableLikeDeclaration.Type { get; set; } = default!;
}
