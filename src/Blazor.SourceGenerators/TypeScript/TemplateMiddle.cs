// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TemplateMiddle : LiteralLikeNode
{
    internal TemplateMiddle() => ((INode)this).Kind = CommentKind.TemplateMiddle;
}