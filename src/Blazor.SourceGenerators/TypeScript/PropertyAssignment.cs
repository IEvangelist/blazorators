// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class PropertyAssignment : ObjectLiteralElement, IObjectLiteralElementLike, IVariableLikeDeclaration
{
    internal PropertyAssignment() => ((INode)this).Kind = CommentKind.PropertyAssignment;

    internal QuestionToken QuestionToken { get; set; }
    internal IExpression Initializer { get; set; }
    internal IPropertyName PropertyName { get; set; }
    internal DotDotDotToken DotDotDotToken { get; set; }
    internal ITypeNode Type { get; set; }
}
