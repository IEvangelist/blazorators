// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NamespaceImport : Declaration, INamedImportBindings
{
    public NamespaceImport()
    {
        Kind = TypeScriptSyntaxKind.NamespaceImport;
    }
}