// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PostfixUnaryExpression : IncrementExpression
{
    public PostfixUnaryExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.PostfixUnaryExpression;

    public IExpression Operand? { get; set; }
    public TypeScriptSyntaxKind Operator? { get; set; }
}