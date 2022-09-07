// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class YieldExpression : Expression
{
    internal YieldExpression() => ((INode)this).Kind = CommentKind.YieldExpression;

    internal AsteriskToken AsteriskToken { get; set; }
    internal IExpression Expression { get; set; }
}