// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class BreakStatement : Statement, IBreakOrContinueStatement
{
    public BreakStatement()
    {
        Kind = TypeScriptSyntaxKind.BreakStatement;
    }

    public Identifier Label { get; set; }
}