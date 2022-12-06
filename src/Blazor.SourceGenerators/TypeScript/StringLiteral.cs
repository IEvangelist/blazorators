// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class StringLiteral : LiteralExpression, IPropertyName
{
    public StringLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.StringLiteral;

    public Node TextSourceNode { get; set; } // Identifier | StringLiteral | NumericLiteral
}