// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TemplateTail : LiteralLikeNode
{
    public TemplateTail() => ((INode)this).Kind = TypeScriptSyntaxKind.TemplateTail;
}