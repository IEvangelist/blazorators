// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NumericLiteral : LiteralExpression, IPropertyName
{
    internal NumericLiteral() => ((INode)this).Kind = CommentKind.NumericLiteral;
}