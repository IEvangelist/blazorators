// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TemplateTail : LiteralLikeNode
{
    internal TemplateTail() => ((INode)this).Kind = CommentKind.TemplateTail;
}