// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ShorthandPropertyAssignment : ObjectLiteralElement, IObjectLiteralElementLike
{
    internal ShorthandPropertyAssignment() => ((INode)this).Kind = CommentKind.ShorthandPropertyAssignment;

    internal QuestionToken QuestionToken { get; set; }
    internal Token EqualsToken { get; set; } // Token<SyntaxKind.EqualsToken>
    internal IExpression ObjectAssignmentInitializer { get; set; }
}
