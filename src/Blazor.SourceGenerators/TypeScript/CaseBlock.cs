// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class CaseBlock : Node
{
    public CaseBlock() => ((INode)this).Kind = TypeScriptSyntaxKind.CaseBlock;

    public NodeArray<ICaseOrDefaultClause> Clauses? { get; set; }
}