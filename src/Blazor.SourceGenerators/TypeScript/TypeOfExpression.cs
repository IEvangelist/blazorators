// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeOfExpression : UnaryExpression
{
    internal TypeOfExpression() => ((INode)this).Kind = SyntaxKind.TypeOfExpression;

    internal IExpression Expression { get; set; }
}