// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class DebuggerStatement : Statement
{
    public DebuggerStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.DebuggerStatement;
}