// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NamespaceImport : Declaration, INamedImportBindings
{
    internal NamespaceImport() => ((INode)this).Kind = CommentKind.NamespaceImport;
}