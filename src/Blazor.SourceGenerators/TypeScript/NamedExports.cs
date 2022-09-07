// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NamedExports : Node, INamedImportsOrExports
{
    internal NamedExports() => ((INode)this).Kind = CommentKind.NamedExports;

    internal NodeArray<ExportSpecifier> Elements { get; set; }
}