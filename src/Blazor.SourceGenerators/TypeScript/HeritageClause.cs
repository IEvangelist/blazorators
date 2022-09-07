// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class HeritageClause : Node
{
    internal HeritageClause() => ((INode)this).Kind = CommentKind.HeritageClause;

    internal CommentKind Token { get; set; } = default!;
    internal NodeArray<ExpressionWithTypeArguments> Types { get; set; } = default!;
}