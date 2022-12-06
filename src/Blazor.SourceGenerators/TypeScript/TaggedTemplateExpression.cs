// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TaggedTemplateExpression : MemberExpression
{
    public TaggedTemplateExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.TaggedTemplateExpression;

    public IExpression Tag { get; set; } //LeftHandSideExpression
    public Node Template { get; set; } //TemplateLiteral
}