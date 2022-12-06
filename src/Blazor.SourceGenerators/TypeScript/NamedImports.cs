// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NamedImports : Node, INamedImportsOrExports, INamedImportBindings
{
    public NamedImports() => ((INode)this).Kind = TypeScriptSyntaxKind.NamedImports;

    public NodeArray<ImportSpecifier> Elements { get; set; }
}