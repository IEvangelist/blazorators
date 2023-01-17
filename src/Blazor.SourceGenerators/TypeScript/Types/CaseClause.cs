// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CaseClause : Node, ICaseOrDefaultClause
{
    public CaseClause()
    {
        Kind = TypeScriptSyntaxKind.CaseClause;
    }

    public IExpression Expression { get; set; }
    public NodeArray<IStatement> Statements { get; set; }
}