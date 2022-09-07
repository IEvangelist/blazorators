// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class CaseBlock : Node
{
    internal CaseBlock() => ((INode)this).Kind = CommentKind.CaseBlock;

    internal NodeArray<ICaseOrDefaultClause> Clauses { get; set; } = default!;
}