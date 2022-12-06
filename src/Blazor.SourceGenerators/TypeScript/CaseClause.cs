// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class CaseClause : Node, ICaseOrDefaultClause
{
    public CaseClause() => ((INode)this).Kind = TypeScriptSyntaxKind.CaseClause;

    public IExpression Expression? { get; set; }
    public NodeArray<IStatement> Statements? { get; set; }
}