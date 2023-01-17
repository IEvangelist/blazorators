// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ShorthandPropertyAssignment : ObjectLiteralElement, IObjectLiteralElementLike
{
    public ShorthandPropertyAssignment()
    {
        Kind = TypeScriptSyntaxKind.ShorthandPropertyAssignment;
    }

    public QuestionToken QuestionToken { get; set; }
    public Token EqualsToken { get; set; }
    public IExpression ObjectAssignmentInitializer { get; set; }
}