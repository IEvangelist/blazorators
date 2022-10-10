// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ParenthesizedTypeNode : TypeNode
{
    internal ParenthesizedTypeNode() => ((INode)this).Kind = SyntaxKind.ParenthesizedType;

    internal ITypeNode Type { get; set; }
}