// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class LiteralTypeNode : TypeNode
{
    internal LiteralTypeNode() => ((INode)this).Kind = SyntaxKind.LiteralType;

    internal IExpression Literal { get; set; }
}