// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class DeleteExpression : UnaryExpression
{
    public DeleteExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.DeleteExpression;

    public IExpression Expression? { get; set; }
}