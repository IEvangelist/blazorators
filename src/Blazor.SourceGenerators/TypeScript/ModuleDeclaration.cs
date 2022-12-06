// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ModuleDeclaration : DeclarationStatement
{
    public ModuleDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ModuleDeclaration;

    public INode? Body { get; set; }
}