// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NamespaceImport : Declaration, INamedImportBindings
{
    public NamespaceImport() => ((INode)this).Kind = TypeScriptSyntaxKind.NamespaceImport;
}