// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TryStatement : Statement
{
    public TryStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.TryStatement;

    public Block TryBlock { get; set; }
    public CatchClause CatchClause { get; set; }
    public Block FinallyBlock { get; set; }
}