// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class SwitchStatement : Statement
{
    public SwitchStatement()
    {
        Kind = TypeScriptSyntaxKind.SwitchStatement;
    }

    public IExpression Expression { get; set; }
    public CaseBlock CaseBlock { get; set; }
    public bool PossiblyExhaustive { get; set; }
}