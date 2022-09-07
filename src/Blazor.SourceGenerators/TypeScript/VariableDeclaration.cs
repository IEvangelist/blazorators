// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class VariableDeclaration : Declaration, IVariableLikeDeclaration
{
    internal VariableDeclaration() => ((INode)this).Kind = CommentKind.VariableDeclaration;

    internal ITypeNode Type { get; set; }
    internal IExpression Initializer { get; set; }
    internal IPropertyName PropertyName { get; set; }
    internal DotDotDotToken DotDotDotToken { get; set; }
    internal QuestionToken QuestionToken { get; set; }
}
