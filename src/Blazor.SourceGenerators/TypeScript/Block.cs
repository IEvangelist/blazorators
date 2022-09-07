// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Block : Statement, IBlockOrExpression
{
    internal Block() => ((INode)this).Kind = CommentKind.Block;

    internal NodeArray<IStatement> Statements { get; set; } = default!;
    internal bool MultiLine { get; set; }
}