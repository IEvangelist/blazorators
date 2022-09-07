// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class CaseClause : Node, ICaseOrDefaultClause
{
    internal CaseClause() => ((INode)this).Kind = CommentKind.CaseClause;

    internal IExpression Expression { get; set; } = default!;
    internal NodeArray<IStatement> Statements { get; set; } = default!;
}