// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class RegularExpressionLiteral : LiteralExpression
{
    internal RegularExpressionLiteral() => ((INode)this).Kind = CommentKind.RegularExpressionLiteral;
}