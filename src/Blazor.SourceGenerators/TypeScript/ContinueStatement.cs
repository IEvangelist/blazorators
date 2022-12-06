// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ContinueStatement : Statement, IBreakOrContinueStatement
{
    public ContinueStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ContinueStatement;

    Identifier IBreakOrContinueStatement.Label? { get; set; }
}