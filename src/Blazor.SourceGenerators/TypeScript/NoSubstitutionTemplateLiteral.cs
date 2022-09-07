// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NoSubstitutionTemplateLiteral : LiteralExpression
{
    internal NoSubstitutionTemplateLiteral() => ((INode)this).Kind = CommentKind.NoSubstitutionTemplateLiteral;
}