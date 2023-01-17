// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExportSpecifier : Declaration, IImportOrExportSpecifier
{
    public ExportSpecifier()
    {
        Kind = TypeScriptSyntaxKind.ExportSpecifier;
    }

    public Identifier PropertyName { get; set; }
}