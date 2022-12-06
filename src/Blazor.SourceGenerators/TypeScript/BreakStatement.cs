// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class BreakStatement : Statement, IBreakOrContinueStatement
{
    public BreakStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.BreakStatement;

    Identifier IBreakOrContinueStatement.Label? { get; set; }
}