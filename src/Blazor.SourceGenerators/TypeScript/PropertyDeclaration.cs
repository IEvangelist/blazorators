// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class PropertyDeclaration : ClassElement, IVariableLikeDeclaration
{
    internal PropertyDeclaration() => ((INode)this).Kind = CommentKind.PropertyDeclaration;

    internal QuestionToken QuestionToken { get; set; }
    internal ITypeNode Type { get; set; }
    internal IExpression Initializer { get; set; }
    internal IPropertyName PropertyName { get; set; }
    internal DotDotDotToken DotDotDotToken { get; set; }
}
