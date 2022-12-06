// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PrefixUnaryExpression : IncrementExpression
{
    public PrefixUnaryExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.PrefixUnaryExpression;

    public TypeScriptSyntaxKind Operator { get; set; }
    public IExpression Operand { get; set; }
}