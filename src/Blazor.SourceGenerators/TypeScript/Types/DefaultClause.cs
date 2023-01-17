// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class DefaultClause : Node, ICaseOrDefaultClause
{
    public DefaultClause()
    {
        Kind = TypeScriptSyntaxKind.DefaultClause;
    }

    public NodeArray<IStatement> Statements { get; set; }
}