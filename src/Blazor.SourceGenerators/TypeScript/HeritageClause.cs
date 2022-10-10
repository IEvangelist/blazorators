// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class HeritageClause : Node
{
    internal HeritageClause() => ((INode)this).Kind = SyntaxKind.HeritageClause;

    internal SyntaxKind Token { get; set; } = default!;
    internal NodeArray<ExpressionWithTypeArguments> Types { get; set; } = default!;
}