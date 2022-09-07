// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TemplateExpression : PrimaryExpression
{
    internal TemplateExpression() => ((INode)this).Kind = CommentKind.TemplateExpression;

    internal TemplateHead Head { get; set; }
    internal NodeArray<TemplateSpan> TemplateSpans { get; set; }
}