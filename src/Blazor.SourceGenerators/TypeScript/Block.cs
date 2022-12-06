// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class Block : Statement, IBlockOrExpression
{
    public Block() => ((INode)this).Kind = TypeScriptSyntaxKind.Block;

    public NodeArray<IStatement> Statements? { get; set; }
    public bool MultiLine { get; set; }
}