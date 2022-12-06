// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NoSubstitutionTemplateLiteral : LiteralExpression
{
    public NoSubstitutionTemplateLiteral() => ((INode)this).Kind = TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral;
}