// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExpressionWithTypeArguments : TypeNode
{
    internal ExpressionWithTypeArguments() => ((INode)this).Kind = CommentKind.ExpressionWithTypeArguments;

    internal /*LeftHandSideExpression*/IExpression Expression { get; set; }
    internal NodeArray<ITypeNode> TypeArguments { get; set; }
}