// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class SpreadElement : Expression
{
    internal SpreadElement() => ((INode)this).Kind = CommentKind.SpreadElement;

    internal IExpression Expression { get; set; } = default!;
}