// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeOperatorNode : ParenthesizedTypeNode
{
    internal TypeOperatorNode() => ((INode)this).Kind = SyntaxKind.TypeOperator;

    internal SyntaxKind Operator { get; set; } = SyntaxKind.KeyOfKeyword;
}