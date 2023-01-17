// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CatchClause : Node
{
    public CatchClause()
    {
        Kind = TypeScriptSyntaxKind.CatchClause;
    }

    public VariableDeclaration VariableDeclaration { get; set; }
    public Block Block { get; set; }
}