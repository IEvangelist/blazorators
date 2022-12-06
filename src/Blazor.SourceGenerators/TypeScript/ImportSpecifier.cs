// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ImportSpecifier : Declaration, ImportOrExportSpecifier
{
    public ImportSpecifier() => ((INode)this).Kind = TypeScriptSyntaxKind.ImportSpecifier;

    public Identifier PropertyName { get; set; }
}