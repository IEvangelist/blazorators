// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TemplateSpan : Node
{
    public TemplateSpan() => ((INode)this).Kind = TypeScriptSyntaxKind.TemplateSpan;

    public IExpression Expression { get; set; }
    public ILiteralLikeNode Literal { get; set; } // TemplateMiddle | TemplateTail
}