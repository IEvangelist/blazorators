// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NamespaceExportDeclaration : DeclarationStatement
{
    internal NamespaceExportDeclaration() => ((INode)this).Kind = CommentKind.NamespaceExportDeclaration;
}