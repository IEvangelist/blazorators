// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ModuleDeclaration : DeclarationStatement
{
    internal ModuleDeclaration() => ((INode)this).Kind = CommentKind.ModuleDeclaration;

    internal /*ModuleDeclaration*/INode Body { get; set; } // ModuleBody | JSDocNamespaceDeclaration
}