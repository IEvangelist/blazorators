// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ImportClause : Declaration
{
    public ImportClause() => ((INode)this).Kind = TypeScriptSyntaxKind.ImportClause;

    public INamedImportBindings NamedBindings { get; set; }
}