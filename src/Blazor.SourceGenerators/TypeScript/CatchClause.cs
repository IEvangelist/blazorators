// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class CatchClause : Node
{
    public CatchClause() => ((INode)this).Kind = TypeScriptSyntaxKind.CatchClause;

    public VariableDeclaration VariableDeclaration? { get; set; }
    public Block Block? { get; set; }
}