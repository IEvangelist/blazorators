// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ParenthesizedTypeNode : TypeNode
{
    public ParenthesizedTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.ParenthesizedType;

    public ITypeNode Type { get; set; }
}