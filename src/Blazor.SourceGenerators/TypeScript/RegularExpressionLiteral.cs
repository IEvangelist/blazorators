// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class RegularExpressionLiteral : LiteralExpression
{
    public RegularExpressionLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.RegularExpressionLiteral;
}