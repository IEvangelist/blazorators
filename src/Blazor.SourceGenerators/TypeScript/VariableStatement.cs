// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class VariableStatement : Statement
{
    public VariableStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.VariableStatement;

    public IVariableDeclarationList DeclarationList { get; set; }
}