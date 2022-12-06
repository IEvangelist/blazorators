// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TemplateMiddle : LiteralLikeNode
{
    public TemplateMiddle() => ((INode)this).Kind = TypeScriptSyntaxKind.TemplateMiddle;
}