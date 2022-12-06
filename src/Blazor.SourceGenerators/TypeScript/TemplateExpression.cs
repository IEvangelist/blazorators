// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TemplateExpression : PrimaryExpression
{
    public TemplateExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.TemplateExpression;

    public TemplateHead Head { get; set; }
    public NodeArray<TemplateSpan> TemplateSpans { get; set; }
}