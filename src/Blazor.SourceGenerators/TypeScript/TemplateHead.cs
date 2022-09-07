// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TemplateHead : LiteralLikeNode
{
    internal TemplateHead() => ((INode)this).Kind = CommentKind.TemplateHead;
}