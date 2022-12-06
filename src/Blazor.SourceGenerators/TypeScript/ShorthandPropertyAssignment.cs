// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ShorthandPropertyAssignment : ObjectLiteralElement, IObjectLiteralElementLike
{
    public ShorthandPropertyAssignment() => ((INode)this).Kind = TypeScriptSyntaxKind.ShorthandPropertyAssignment;

    public QuestionToken QuestionToken { get; set; }
    public Token EqualsToken { get; set; } // Token<TypeScriptSyntaxKind.EqualsToken>
    public IExpression ObjectAssignmentInitializer { get; set; }
}
