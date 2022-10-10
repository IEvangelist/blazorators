// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TemplateSpan : Node
{
    internal TemplateSpan() => ((INode)this).Kind = SyntaxKind.TemplateSpan;

    internal IExpression Expression { get; set; }
    internal ILiteralLikeNode Literal { get; set; } // TemplateMiddle | TemplateTail
}