// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeOfExpression : UnaryExpression
{
    public TypeOfExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeOfExpression;

    public IExpression Expression { get; set; }
}