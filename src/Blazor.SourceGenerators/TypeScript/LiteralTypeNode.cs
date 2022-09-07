// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class LiteralTypeNode : TypeNode
{
    internal LiteralTypeNode() => ((INode)this).Kind = CommentKind.LiteralType;

    internal IExpression Literal { get; set; }
}