// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ExportSpecifier : Declaration, ImportOrExportSpecifier
{
    public ExportSpecifier() => ((INode)this).Kind = TypeScriptSyntaxKind.ExportSpecifier;

    Identifier ImportOrExportSpecifier.PropertyName? { get; set; }
}