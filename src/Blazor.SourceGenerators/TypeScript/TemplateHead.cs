// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TemplateHead : LiteralLikeNode
{
    public TemplateHead() => ((INode)this).Kind = TypeScriptSyntaxKind.TemplateHead;
}