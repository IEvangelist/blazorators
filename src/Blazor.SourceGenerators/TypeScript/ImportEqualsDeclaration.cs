// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ImportEqualsDeclaration : DeclarationStatement
{
    public ImportEqualsDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.ImportEqualsDeclaration;

    public INode ModuleReference? { get; set; }
}