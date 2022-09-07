// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ElementAccessExpression : MemberExpression
{
    internal ElementAccessExpression() => ((INode)this).Kind = CommentKind.ElementAccessExpression;

    internal IExpression Expression { get; set; } = default!;
    internal IExpression ArgumentExpression { get; set; } = default!;
}