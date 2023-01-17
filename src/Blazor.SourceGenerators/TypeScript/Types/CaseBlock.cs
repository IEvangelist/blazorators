// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CaseBlock : Node
{
    public CaseBlock()
    {
        Kind = TypeScriptSyntaxKind.CaseBlock;
    }

    public NodeArray<ICaseOrDefaultClause> Clauses { get; set; }
}