// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NamedImports : Node, INamedImportsOrExports, INamedImportBindings
{
    public NamedImports()
    {
        Kind = TypeScriptSyntaxKind.NamedImports;
    }

    public NodeArray<ImportSpecifier> Elements { get; set; }
}