// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SwitchStatement : Statement
{
    public SwitchStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.SwitchStatement;

    public IExpression Expression { get; set; }
    public CaseBlock CaseBlock { get; set; }
    public bool PossiblyExhaustive { get; set; }
}