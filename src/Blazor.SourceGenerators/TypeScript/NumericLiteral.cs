// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NumericLiteral : LiteralExpression, IPropertyName
{
    public NumericLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.NumericLiteral;
}