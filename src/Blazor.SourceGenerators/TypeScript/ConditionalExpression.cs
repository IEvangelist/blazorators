// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ConditionalExpression : Expression
{
    internal ConditionalExpression() => ((INode)this).Kind = CommentKind.ConditionalExpression;

    internal IExpression Condition { get; set; }
    internal QuestionToken QuestionToken { get; set; }
    internal IExpression WhenTrue { get; set; }
    internal ColonToken ColonToken { get; set; }
    internal IExpression WhenFalse { get; set; }
}