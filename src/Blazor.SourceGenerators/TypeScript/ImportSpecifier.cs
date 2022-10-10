// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ImportSpecifier : Declaration, ImportOrExportSpecifier
{
    internal ImportSpecifier() => ((INode)this).Kind = SyntaxKind.ImportSpecifier;

    internal Identifier PropertyName { get; set; }
}