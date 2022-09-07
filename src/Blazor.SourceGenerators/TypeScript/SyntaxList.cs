// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SyntaxList : Node
{
    internal SyntaxList() => ((INode)this).Kind = CommentKind.SyntaxList;

    internal Node[] _children { get; set; }
}