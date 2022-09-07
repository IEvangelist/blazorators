// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeAssertion : UnaryExpression
{
    internal TypeAssertion() => ((INode)this).Kind = CommentKind.TypeAssertionExpression;

    internal ITypeNode Type { get; set; }
    internal IExpression Expression { get; set; }
}