// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ModuleDeclaration : DeclarationStatement
{
    internal ModuleDeclaration() => ((INode)this).Kind = SyntaxKind.ModuleDeclaration;

    internal INode? Body { get; set; }
}