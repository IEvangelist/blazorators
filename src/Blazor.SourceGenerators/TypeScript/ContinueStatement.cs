// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ContinueStatement : Statement, IBreakOrContinueStatement
{
    internal ContinueStatement() => ((INode)this).Kind = CommentKind.ContinueStatement;

    Identifier IBreakOrContinueStatement.Label { get; set; } = default!;
}