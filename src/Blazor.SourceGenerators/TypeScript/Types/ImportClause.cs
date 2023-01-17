// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ImportClause : Declaration
{
    public ImportClause()
    {
        Kind = TypeScriptSyntaxKind.ImportClause;
    }

    public INamedImportBindings NamedBindings { get; set; }
}