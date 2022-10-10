// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ExportSpecifier : Declaration, ImportOrExportSpecifier
{
    internal ExportSpecifier() => ((INode)this).Kind = SyntaxKind.ExportSpecifier;

    Identifier ImportOrExportSpecifier.PropertyName { get; set; } = default!;
}