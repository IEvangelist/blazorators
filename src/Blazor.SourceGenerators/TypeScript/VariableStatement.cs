// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class VariableStatement : Statement
{
    internal VariableStatement() => ((INode)this).Kind = SyntaxKind.VariableStatement;

    internal IVariableDeclarationList DeclarationList { get; set; }
}