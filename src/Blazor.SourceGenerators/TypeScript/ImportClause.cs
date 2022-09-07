// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ImportClause : Declaration
{
    internal ImportClause() => ((INode)this).Kind = CommentKind.ImportClause;

    internal INamedImportBindings NamedBindings { get; set; }
}