// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TaggedTemplateExpression : MemberExpression
{
    internal TaggedTemplateExpression() => ((INode)this).Kind = CommentKind.TaggedTemplateExpression;

    internal IExpression Tag { get; set; } //LeftHandSideExpression
    internal Node Template { get; set; } //TemplateLiteral
}