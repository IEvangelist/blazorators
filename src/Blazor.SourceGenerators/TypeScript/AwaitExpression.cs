// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class AwaitExpression : UnaryExpression
{
    internal AwaitExpression() => ((INode)this).Kind = CommentKind.AwaitExpression;

    internal IExpression Expression { get; set; } = default!;
}