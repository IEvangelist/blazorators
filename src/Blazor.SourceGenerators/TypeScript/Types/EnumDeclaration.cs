// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EnumDeclaration : DeclarationStatement
{
    public EnumDeclaration()
    {
        Kind = TypeScriptSyntaxKind.EnumDeclaration;
    }

    public NodeArray<EnumMember> Members { get; set; }
}