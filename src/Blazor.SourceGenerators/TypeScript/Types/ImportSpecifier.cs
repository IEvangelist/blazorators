// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ImportSpecifier : Declaration, IImportOrExportSpecifier
{
    public ImportSpecifier()
    {
        Kind = TypeScriptSyntaxKind.ImportSpecifier;
    }

    public Identifier PropertyName { get; set; }
}