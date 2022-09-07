// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class OmittedExpression : Expression, IArrayBindingElement
{
    internal OmittedExpression() => ((INode)this).Kind = CommentKind.OmittedExpression;
}