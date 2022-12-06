// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NamespaceExportDeclaration : DeclarationStatement
{
    public NamespaceExportDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.NamespaceExportDeclaration;
}