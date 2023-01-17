// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ContinueStatement : Statement, IBreakOrContinueStatement
{
    public ContinueStatement()
    {
        Kind = TypeScriptSyntaxKind.ContinueStatement;
    }

    public Identifier Label { get; set; }
}