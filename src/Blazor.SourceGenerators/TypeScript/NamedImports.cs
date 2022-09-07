// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NamedImports : Node, INamedImportsOrExports, INamedImportBindings
{
    internal NamedImports() => ((INode)this).Kind = CommentKind.NamedImports;

    internal NodeArray<ImportSpecifier> Elements { get; set; }
}