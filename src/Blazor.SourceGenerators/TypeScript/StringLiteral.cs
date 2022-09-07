// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class StringLiteral : LiteralExpression, IPropertyName
{
    internal StringLiteral() => ((INode)this).Kind = CommentKind.StringLiteral;

    internal Node TextSourceNode { get; set; } // Identifier | StringLiteral | NumericLiteral
}