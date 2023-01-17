// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NamedExports : Node, INamedImportsOrExports
{
    public NamedExports()
    {
        Kind = TypeScriptSyntaxKind.NamedExports;
    }

    public NodeArray<ExportSpecifier> Elements { get; set; }
}