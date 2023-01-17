// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ImportEqualsDeclaration : DeclarationStatement
{
    public ImportEqualsDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ImportEqualsDeclaration;
    }

    public /*ModuleReference*/ INode ModuleReference { get; set; }
}