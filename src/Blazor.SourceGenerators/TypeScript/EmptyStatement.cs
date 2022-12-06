// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EmptyStatement : Statement
{
    public EmptyStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.EmptyStatement;
}