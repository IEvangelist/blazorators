// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class BreakStatement : Statement, IBreakOrContinueStatement
{
    internal BreakStatement() => ((INode)this).Kind = CommentKind.BreakStatement;

    Identifier IBreakOrContinueStatement.Label { get; set; } = default!;
}