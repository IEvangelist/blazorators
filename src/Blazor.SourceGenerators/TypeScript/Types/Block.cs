// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class Block : Statement, IBlockOrExpression
{
    public Block()
    {
        Kind = TypeScriptSyntaxKind.Block;
    }

    public NodeArray<IStatement> Statements { get; set; }
    public bool MultiLine { get; set; }
}