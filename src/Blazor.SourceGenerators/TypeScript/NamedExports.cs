// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NamedExports : Node, INamedImportsOrExports
{
    public NamedExports() => ((INode)this).Kind = TypeScriptSyntaxKind.NamedExports;

    public NodeArray<ExportSpecifier> Elements { get; set; }
}