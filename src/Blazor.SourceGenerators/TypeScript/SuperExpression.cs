// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SuperExpression : PrimaryExpression
{
    public SuperExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.SuperKeyword;
}