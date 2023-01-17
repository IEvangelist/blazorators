// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class VariableDeclarationList : Node, IVariableDeclarationList
{
    public VariableDeclarationList()
    {
        Kind = TypeScriptSyntaxKind.VariableDeclarationList;
    }

    public NodeArray<VariableDeclaration> Declarations { get; set; }
}