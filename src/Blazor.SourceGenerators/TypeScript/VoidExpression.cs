// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class VoidExpression : UnaryExpression
{
    internal VoidExpression() => ((INode)this).Kind = SyntaxKind.VoidExpression;

    internal IExpression Expression { get; set; }
}