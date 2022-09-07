// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ArrayLiteralExpression : PrimaryExpression
{
    internal ArrayLiteralExpression() => ((INode)this).Kind = CommentKind.ArrayLiteralExpression;

    internal NodeArray<IExpression> Elements { get; set; } = default!;
    internal bool MultiLine { get; set; }
}