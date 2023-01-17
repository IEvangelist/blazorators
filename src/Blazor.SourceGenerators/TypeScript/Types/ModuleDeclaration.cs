// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ModuleDeclaration : DeclarationStatement
{
    public ModuleDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ModuleDeclaration;
    }

    public INode Body { get; set; }
}