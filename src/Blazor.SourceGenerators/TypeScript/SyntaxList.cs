// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SyntaxList : Node
{
    public SyntaxList() => ((INode)this).Kind = TypeScriptSyntaxKind.SyntaxList;

    public Node[] _children { get; set; }
}