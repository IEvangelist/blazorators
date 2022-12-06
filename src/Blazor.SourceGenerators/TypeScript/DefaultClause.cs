// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class DefaultClause : Node, ICaseOrDefaultClause
{
    public DefaultClause() => ((INode)this).Kind = TypeScriptSyntaxKind.DefaultClause;

    public NodeArray<IStatement> Statements { get; set; } = NodeArray<IStatement>.Empty;
}